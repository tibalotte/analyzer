-- Real FFB Haptics v1.3 by Gian Carlo P.

-- Read both sections as tables
local settings = ac.INIConfig.scriptSettings():mapSection('SETTINGS', {
    suspTravelK = 0.22,
    dampK = 0.05,
    roadTextureIntensityK = 0.5,
    roadTextureFreqK = 45,
    interpolationActiveK = 1,
    interpolationRateK = 500,
    interpolationSmoothingK = 0.15,
    interpolationModeK = 1,
    aeroActiveK = 1,
    aeroDampingIntensityK = 0.25,
    aeroLoadK = 0.2
})

local haptics = ac.INIConfig.scriptSettings():mapSection('HAPTICS', {
    joltActiveK = 1,
    joltForceK = 0.30,
    joltDurationK = 85,
    absActiveK = 1,
    absIntensityK = 0.25,
    absFrequencyK = 40,
    tcActiveK = 1,
    tcIntensityK = 0.55,
    tcFrequencyK = 120,
    spinActiveK = 1,
    spinIntensityK = 0.22,
    spinFrequencyK = 50,
    lockActiveK = 1,
    lockIntensityK = 0.30,
    lockFrequencyK = 45,
    lateralSlipActiveK = 1,
    lateralSlipIntensityK = 0.14,
    lateralSlipFrequencyK = 30,
    understeerIntensityK = 0.40,
    understeerFrequencyK = 40,
    oversteerIntensityK = 0.40,
    oversteerFrequencyK = 40,
    kerbActiveK = 1,
    kerbIntensityK = 0.4,
    tyreDirtActiveK = 1,
    tyreDirtIntensityK = 0.4,
    stationaryScrubActiveK = 1,
    stationaryScrubIntensityK = 0.26,
    stationaryScrubFrequencyK = 32,
    weightTransferActiveK = 1,
    weightTransferIntensityK = 0.25
})

-- Merge both tables into ScriptSettings
ScriptSettings = {}
for k, v in pairs(settings) do ScriptSettings[k] = v end
for k, v in pairs(haptics) do ScriptSettings[k] = v end

ac.debug('settings', ScriptSettings)

-- Interpolation System
local interpolationSystem = {
    enabled = false,
    targetRate = 1000,
    actualRate = 333, 
    smoothing = 0.15,
    mode = 1,
    lastFfbValue = 0,
    lastFfbFinal = 0,
    history = {},
    historySize = 4
}

-- Initialize interpolation history
local function initInterpolationHistory()
    for i = 1, interpolationSystem.historySize do
        interpolationSystem.history[i] = {
            ffbValue = 0,
            ffbFinal = 0,
            timestamp = 0
        }
    end
end

-- Linear interpolation
local function lerp(a, b, t)
    return a + (b - a) * t
end

-- Cubic interpolation (Catmull-Rom)
local function cubicInterp(p0, p1, p2, p3, t)
    local t2 = t * t
    local t3 = t2 * t
    return 0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3)
end

-- Bezier interpolation
local function bezierInterp(p0, p1, p2, p3, t)
    local u = 1 - t
    local uu = u * u
    local uuu = uu * u
    local tt = t * t
    local ttt = tt * t
    return uuu * p0 + 3 * uu * t * p1 + 3 * u * tt * p2 + ttt * p3
end

-- Update interpolation history
local function updateInterpolationHistory(ffbValue, ffbFinal, currentTime)
    for i = interpolationSystem.historySize, 2, -1 do
        interpolationSystem.history[i] = interpolationSystem.history[i - 1]
    end
    interpolationSystem.history[1] = {
        ffbValue = ffbValue,
        ffbFinal = ffbFinal,
        timestamp = currentTime
    }
end

-- Get interpolated FFB value
local function getInterpolatedFFB(currentFfbValue, currentFfbFinal, dt)
    local mode = interpolationSystem.mode
    local history = interpolationSystem.history
    local progress = math.min(1, dt * interpolationSystem.targetRate)

    local prevTotal = interpolationSystem.lastFfbValue + interpolationSystem.lastFfbFinal
    local currentTotal = currentFfbValue + currentFfbFinal

    if mode == 1 then -- Linear
        return lerp(prevTotal, currentTotal, progress)

    elseif mode == 2 and #history >= 4 then -- Cubic
        local h = history
        local p0 = h[2].ffbValue + h[2].ffbFinal
        local p1 = h[1].ffbValue + h[1].ffbFinal
        local p2 = currentTotal
        local p3 = currentTotal + (currentTotal - p1)  -- Extrapolate
        return cubicInterp(p0, p1, p2, p3, progress)

    elseif mode == 3 and #history >= 4 then -- Bezier
        local h = history
        return bezierInterp(
            h[4].ffbValue + h[4].ffbFinal,
            h[3].ffbValue + h[3].ffbFinal,
            h[2].ffbValue + h[2].ffbFinal,
            currentTotal,
            progress
        )

    else -- Fallback to linear
        return lerp(prevTotal, currentTotal, progress)
    end
end

-- Update interpolation each physics tick
local function updateInterpolation(ffbValue, ffbFinal, dt)
    if not interpolationSystem.enabled then return ffbFinal end

    updateInterpolationHistory(ffbValue, ffbFinal, simTime)
    
    local interpolated = getInterpolatedFFB(ffbValue, ffbFinal, dt)

    local sm = interpolationSystem.smoothing
    local final = lerp(interpolationSystem.lastFfbFinal, interpolated, 1 - sm)

    interpolationSystem.lastFfbValue = ffbValue
    interpolationSystem.lastFfbFinal = final

    return final
end

function script.init()
    interpolationSystem.enabled = ScriptSettings.interpolationActiveK == 1
    interpolationSystem.targetRate = math.max(333, math.min(1000, ScriptSettings.interpolationRateK))
    interpolationSystem.smoothing = math.max(0, math.min(1, ScriptSettings.interpolationSmoothingK))
    interpolationSystem.mode = math.max(1, math.min(3, ScriptSettings.interpolationModeK))

    initInterpolationHistory()

    ac.debug('Interpolation System', {
        enabled = interpolationSystem.enabled,
        targetRate = interpolationSystem.targetRate,
        actualRate = interpolationSystem.actualRate,
        smoothing = interpolationSystem.smoothing,
        mode = interpolationSystem.mode
    })
end

-- Setup
local dampSlowSpeedK = 0.45

-- Implementation
local carPh = ac.getCarPhysicsRate()

-- Helpers
if not math.round then
    function math.round(x, n)
        n = n or 0
        local m = 10^n
        return math.floor(x * m + 0.5) / m
    end
end

if not math.sign then
    function math.sign(x) return (x>0 and 1) or (x<0 and -1) or 0 end
end

if not math.smoothstep then
    function math.smoothstep(x)
        x = math.saturate(x)
        return x * x * (3 - 2 * x)
    end
end

local carIni = ac.INIConfig.carData(car.index, 'car.ini')
local carIniFfbMult = math.min(carIni:get('CONTROLS', 'FFMULT', 1), 10)

local lastSuspT = { 0, 0 }
local wheelAngularSpeed = { 0, 0 }
local wheelVelocity = { 0, 0 }
local wheelLoad = { 0, 0, 0, 0 }
local wheelSlipAngle = { 0, 0, 0, 0 }
local wheelSlipRatio = { 0, 0, 0, 0 }
local surfaceMult = { 0, 0 }
local vibrationS = { 0, 0 }
local tyreDirt = { 0, 0}

local carSpeedKmh = 0

-- Session state tracking for damping
local sessionState = {
    inSession = false,
    lastSessionCheck = 0,
    checkInterval = 0.1  -- 100ms 
}
local lastInSession = false

-- Slip haptic variables
local lastSlipType = "neutral"

-- Dirty surface variables
local frontTyreSurface = {0, 0}
local dirtyJoltActive = {false, false}
local dirtyJoltTime = {0, 0}
local dirtySurfaces = {
    [ac.SurfaceExtendedType.Grass] = true,
    [ac.SurfaceExtendedType.Gravel] = true,
    [ac.SurfaceExtendedType.Sand] = true,
    [ac.SurfaceExtendedType.Ice] = true,
    [ac.SurfaceExtendedType.Snow] = true
}

if not suspensionFilter then
    suspensionFilter = {
        lowPass = 0,
        alpha = 0.96
    }
end

-- Weight transfer smoothing filter
if not weightFilter then
    weightFilter = { lat = 0, long = 0, alpha = 0.9 }
end

-- Phase tracking variables
local kerbPhase = {0, 0}
local offroadPhase = {0, 0}
local spinPhase = 0
local lateralSlipPhase = 0
local scrubPhase = 0
local roadTexturePhase = 0 

-- Shift jolt
local carData = ac.getCar(0)
local lastGear = carPh.gear
local gearJoltBaseForce = 0
local joltTime = 0

-- ABS
local shiftPhase = 0
local simTime = 0

local function isInActiveSession()
    if simTime - sessionState.lastSessionCheck < sessionState.checkInterval then
        return sessionState.inSession
    end
    
    sessionState.lastSessionCheck = simTime
    
    -- Session
    local conditions = {
        ac.getSim().isInMainMenu == false,           
        ac.getSim().isSessionStarted == true,        
        carPh ~= nil,                                
        car.isConnected == true,                     
        ac.getSim().isReplayActive == false          
    }
    
    sessionState.inSession = true
    for _, condition in ipairs(conditions) do
        if not condition then
            sessionState.inSession = false
            break
        end
    end
    
    return sessionState.inSession
end

-- Surface helpers
local gravelS = { ac.SurfaceExtendedType.Grass, ac.SurfaceExtendedType.Gravel, ac.SurfaceExtendedType.Sand }
local iceS = { ac.SurfaceExtendedType.Ice, ac.SurfaceExtendedType.Snow }
local function checkSurface(s)
    for i = 1, #gravelS do
        if s == gravelS[i] then
            return 0.5
        end
    end
    for i = 1, #iceS do
        if s == iceS[i] then
            return 2
        end
    end
    return 1
end

local function ffbState(i)
    local function update(dt, wheel, tyre)
        carSpeedKmh = carPh.speedKmh

        if i < 2 then
            local sType = checkSurface(tyre.surfaceExtendedType)
            wheelAngularSpeed[i + 1] = tyre.angularSpeed
            wheelVelocity[i + 1] = tyre.velocity
            wheelLoad[i + 1] = tyre.load
            wheelSlipAngle[i + 1] = tyre.slipAngle
            wheelSlipRatio[i + 1] = tyre.slipRatio
            surfaceMult[i + 1] = sType
            frontTyreSurface[i + 1] = tyre.surfaceExtendedType
            tyreDirt[i + 1] = wheel.tyreDirty
            vibrationS[i + 1] = tyre.surfaceExtendedType == ac.SurfaceExtendedType.Gravel and 1 or 0
        end
        if i > 1 then
            wheelLoad[i + 1] = tyre.load
            wheelSlipAngle[i + 1] = tyre.slipAngle
            wheelSlipRatio[i + 1] = tyre.slipRatio
        end
    end
    return update
end

local wheels = { ffbState(0), ffbState(1), ffbState(2), ffbState(3) }

local ffbCounter = 0
local ffbSwitcher = 1

-- Main FFB calculation function
local function calculateFFBEffects(dt, ffbValue, steerInput, steerInputSpeed)
    -- Settings
    local suspTravelK = ScriptSettings.suspTravelK * 250
    local dampK = ScriptSettings.dampK
    local aeroActiveK = ScriptSettings.aeroActiveK
    local aeroDampingIntensityK = ScriptSettings.aeroDampingIntensityK * 3.2
    local aeroLoadK = ScriptSettings.aeroLoadK * 0.05

    -- Haptics
    local joltActiveK = ScriptSettings.joltActiveK
    local joltForceK = ScriptSettings.joltForceK * 0.6
    local joltDurationK = ScriptSettings.joltDurationK / 1000
    local absActiveK = ScriptSettings.absActiveK
    local absIntensityK = ScriptSettings.absIntensityK * 1.6
    local absFrequencyK = ScriptSettings.absFrequencyK
    local tcActiveK = ScriptSettings.tcActiveK
    local tcIntensityK = ScriptSettings.tcIntensityK * 1.2
    local tcFrequencyK = ScriptSettings.tcFrequencyK
    local spinActiveK = ScriptSettings.spinActiveK
    local spinIntensityK = ScriptSettings.spinIntensityK 
    local spinFrequencyK = ScriptSettings.spinFrequencyK
    local lockActiveK = ScriptSettings.lockActiveK
    local lockIntensityK = ScriptSettings.lockIntensityK
    local lockFrequencyK = ScriptSettings.lockFrequencyK
    local lateralSlipActiveK = ScriptSettings.lateralSlipActiveK
    local lateralSlipIntensityK = ScriptSettings.lateralSlipIntensityK * 0.45
    local lateralSlipFrequencyK = ScriptSettings.lateralSlipFrequencyK
    local understeerIntensityK = ScriptSettings.understeerIntensityK * 1.4
    local understeerFrequencyK = ScriptSettings.understeerFrequencyK
    local oversteerIntensityK = ScriptSettings.oversteerIntensityK * 1.5
    local oversteerFrequencyK = ScriptSettings.oversteerFrequencyK
    local kerbActiveK = ScriptSettings.kerbActiveK
    local kerbIntensityK = ScriptSettings.kerbIntensityK * 1.4
    local tyreDirtActiveK = ScriptSettings.tyreDirtActiveK
    local tyreDirtIntensityK = ScriptSettings.tyreDirtIntensityK * 1.4
    local stationaryScrubActiveK = ScriptSettings.stationaryScrubActiveK
    local stationaryScrubIntensityK = ScriptSettings.stationaryScrubIntensityK * 2.5
    local stationaryScrubFrequencyK = ScriptSettings.stationaryScrubFrequencyK
    local weightTransferActiveK = ScriptSettings.weightTransferActiveK
    local weightTransferIntensityK = ScriptSettings.weightTransferIntensityK

    simTime = simTime + dt

    for i = 1, 4 do
        wheels[i](dt, car.wheels[i - 1], carPh.wheels[i - 1])
    end

    local baseGain = ac.getFFBGain()

    local slowSpeedMult = (1.1 - math.smoothstep(math.saturate((carSpeedKmh - 1) / 50)))

    -- SUSPENSION PARAMETERS
    local roadTextureIntensityK = ScriptSettings.roadTextureIntensityK 
    local roadTextureFreqK = ScriptSettings.roadTextureFreqK
    
    -- Suspension force 
    local suspensionSpeed = { carPh.wheels[0].suspensionTravel - lastSuspT[1], carPh.wheels[1].suspensionTravel - lastSuspT[2] }
    
    -- Asymmetric Effect: reacts to kerbs and bumps on a single side
    local sSpeedDiff = suspensionSpeed[1] - suspensionSpeed[2]
    local ffbSusp_Asymmetric = sSpeedDiff * suspTravelK * (baseGain ^ 0.5)

    -- Road Texture Effect: reacts to the overall suspension work
    local ffbRoadTexture = 0
    -- Calculate the average absolute suspension velocity 
    local avgSuspVelocity = (math.abs(suspensionSpeed[1]) + math.abs(suspensionSpeed[2])) / 2
    
    -- Apply a filter to smooth the signal and make it more natural
    suspensionFilter.lowPass = suspensionFilter.alpha * suspensionFilter.lowPass + (1 - suspensionFilter.alpha) * avgSuspVelocity
    local smoothedAvgVelocity = suspensionFilter.lowPass
    
    if smoothedAvgVelocity > 0.0001 then
        -- The vibration intensity depends on how fast the suspensions are moving
        local amplitude = smoothedAvgVelocity * 150 * roadTextureIntensityK * math.saturate(carSpeedKmh / 130)
        
        -- The vibration frequency slightly increases with suspension speed
        local frequency = roadTextureFreqK + (smoothedAvgVelocity * 2000)
        
        -- Generate the oscillation
        roadTexturePhase = (roadTexturePhase + frequency * dt) % 1
        local oscillation = math.sin(roadTexturePhase * 2 * math.pi)
        
        ffbRoadTexture = (oscillation * amplitude * baseGain) / (1.0 + math.abs(ffbValue))
    end
    
    -- Update the suspension position for the next frame
    lastSuspT = { carPh.wheels[0].suspensionTravel, carPh.wheels[1].suspensionTravel }
    
    -- Combine the two effects
    local ffbSusp = ffbSusp_Asymmetric + ffbRoadTexture
    
    if surfaceMult[1] ~= 1 or surfaceMult[2] ~= 1 then
        ffbSusp = math.abs((1 + ffbSusp) ^ 0.5 - 1) * math.sign(ffbSusp)
    end

    -- Gravel vibration
    local ffbVibrationForce = 0
    local vibrationMult = (vibrationS[1] + vibrationS[2]) / 2
    if vibrationMult > 0 then
        local loadMult = ((math.saturate(wheelLoad[1] / 1000) + math.saturate(wheelLoad[2] / 1000)) / 2) ^ 0.5
        if ffbCounter > 1 then ffbSwitcher = -1
        elseif ffbCounter < -1 then ffbSwitcher = 1 end
        ffbCounter = ffbCounter + 0.0025 * carSpeedKmh * ffbSwitcher * math.random(0, 2)
        ffbCounter = ffbCounter * math.saturate(math.random(0, 5))
        ffbVibrationForce = (ffbCounter * 0.1 * loadMult * vibrationMult * baseGain) / (1 + math.abs(ffbValue))
    end

    -- Kerbs
    local ffbKerbForce = 0
    if kerbActiveK == 1 then
        for i = 1, 2 do
            if frontTyreSurface[i] == 4 then
                local minFreq, maxFreq = 22, 35
                local speedNorm = math.saturate((carSpeedKmh - 10) / (200 - 10))
                local kerbFreq = minFreq + (maxFreq - minFreq) * speedNorm
                kerbPhase[i] = (kerbPhase[i] + kerbFreq * dt) % 1
                local kerbStrength = (0.15 + 0.25 * speedNorm) * kerbIntensityK
                local kerbOsc = math.sin(kerbPhase[i] * 2 * math.pi) * kerbStrength
                if math.abs(ffbSusp) > 0.03 then kerbOsc = kerbOsc * 0.2 end
                ffbKerbForce = ffbKerbForce + (kerbOsc * baseGain) / (1 + math.abs(ffbValue))
            else
                kerbPhase[i] = 0
            end
        end
    end

    -- Offroad Surface Effect
local ffbOffroadForce = 0
local offroadSurfaces = {
    [ac.SurfaceExtendedType.Grass] = { freq = 22, amp = 0.02, amp_speed = 0.4, freq_speed = 0.32 },
    [ac.SurfaceExtendedType.Gravel] = { freq = 50, amp = 0.03, amp_speed = 0.5, freq_speed = 0.55 },
    [ac.SurfaceExtendedType.Sand] = { freq = 20, amp = 0.01, amp_speed = 0.3, freq_speed = 0.25 },
    [ac.SurfaceExtendedType.Ice] = { freq = 10, amp = 0.05, amp_speed = 0.1, freq_speed = 0.2 },
    [ac.SurfaceExtendedType.Snow] = { freq = 12, amp = 0.08, amp_speed = 0.2, freq_speed = 0.25 }
}
for i = 1, 2 do  
    local surface = frontTyreSurface[i]
    if offroadSurfaces[surface] then
        local speedNorm = math.saturate(carSpeedKmh / 160)
        local loadFactor = math.saturate(wheelLoad[i] / 1800)
        local surfaceProps = offroadSurfaces[surface]
        local dynamicFreq = surfaceProps.freq + surfaceProps.freq * speedNorm * surfaceProps.freq_speed
        local dynamicAmp = surfaceProps.amp + surfaceProps.amp * speedNorm * surfaceProps.amp_speed
        local frequency = dynamicFreq * (0.45 + loadFactor * 0.65)
        local amplitude = dynamicAmp * loadFactor        
        offroadPhase[i] = (offroadPhase[i] + frequency * dt) % 1
        local oscillation = math.sin(offroadPhase[i] * 2 * math.pi)
        
        local wheelWeight = (i == 1) and -1 or 1
        local wheelForce = oscillation * amplitude * baseGain * wheelWeight
        ffbOffroadForce = ffbOffroadForce + wheelForce
    else
        offroadPhase[i] = 0
    end
end

    -- Dirty tyre
    local ffbDirtyTyreForce = 0
    if tyreDirtActiveK == 1 then
        local totalDirt = tyreDirt[1] + tyreDirt[2]
        local onDirtyNow = dirtySurfaces[frontTyreSurface[1]] or dirtySurfaces[frontTyreSurface[2]]
        if totalDirt > 0.00 and not onDirtyNow then
            local fade = (math.saturate(totalDirt / 10)) ^ 0.5
            local speedFade = math.saturate(carSpeedKmh / 100)
            local baseStrength = (0.11 * tyreDirtIntensityK) * fade * speedFade
            local sineFreq = math.max(30, 60 * speedFade)
            local joltChance = 2 * fade
            local sine = math.sin(simTime * sineFreq * 2 * math.pi)
            local jitter = 0.9 + math.random() * 0.2
            local dirtyOsc = sine * baseStrength * jitter

            local joltStrength = 0.20 * tyreDirtIntensityK
            local joltDuration = 0.015

            if not dirtyJoltActive[1] and not dirtyJoltActive[2] and math.random() < joltChance * dt then
                dirtyJoltActive[1] = true
                dirtyJoltActive[2] = true
                dirtyJoltTime[1] = 0
                dirtyJoltTime[2] = 0
            end

            local joltOsc = 0
            if dirtyJoltActive[1] or dirtyJoltActive[2] then
                if dirtyJoltTime[1] < joltDuration or dirtyJoltTime[2] < joltDuration then
                    joltOsc = joltStrength * math.sign(sine) * (0.9 + math.random() * 0.2)
                    dirtyJoltTime[1] = dirtyJoltTime[1] + dt
                    dirtyJoltTime[2] = dirtyJoltTime[2] + dt
                else
                    dirtyJoltActive[1] = false
                    dirtyJoltActive[2] = false
                    dirtyJoltTime[1] = 0
                    dirtyJoltTime[2] = 0
                end
            end

            ffbDirtyTyreForce = ((dirtyOsc + joltOsc) * baseGain) / (1 + math.abs(ffbValue))
        else
            dirtyJoltActive[1] = false
            dirtyJoltActive[2] = false
            dirtyJoltTime[1] = 0
            dirtyJoltTime[2] = 0
        end
    end

    -- ABS
    local ffbAbsForce = 0
    if absActiveK == 1 then
        local absLeft = math.abs(carPh.wheels[0].abs)
        local absRight = math.abs(carPh.wheels[1].abs)
        local absVib = absLeft + absRight

        if math.round(absVib, 2) > 0.00 then
            local absDirection = absRight - absLeft
            local absOscillation
            if absDirection > 0 then
                absOscillation = math.max(math.sin(simTime * absFrequencyK * 2 * math.pi), 0)
            elseif absDirection < 0 then
                absOscillation = math.min(math.sin(simTime * absFrequencyK * 2 * math.pi), 0)
            else
                absOscillation = math.sin(simTime * absFrequencyK * 2 * math.pi)
            end
            local jitter = 0.9 + math.random() * 0.25
            ffbAbsForce = (absOscillation * absIntensityK * jitter * baseGain) / (1 + math.abs(ffbValue))
        end
    end

    -- TC
    local ffbTcForce = 0
    if tcActiveK == 1 and carPh.tractionControlInAction then
        local rpmRatio = math.saturate(carPh.rpm / (carData.rpmLimiter or 7000))
        local minFreq = 40
        local maxFreq = tcFrequencyK
        local baseFreq = minFreq + (maxFreq - minFreq) * rpmRatio
        local rumble = 0.35 * math.sin(simTime * baseFreq * 2 * math.pi)
        local buzzFreq = baseFreq * 2.5
        local buzz = 0.5 * math.sin(simTime * buzzFreq * 2 * math.pi)
        ffbTcForce = ((rumble + buzz) * tcIntensityK * baseGain) / (2 + math.abs(ffbValue))
    end

    -- Wheel Lock & Spin
    local wheelLockVib, wheelSpinVib = 0, 0
    for i = 1, 4 do
        if wheelSlipRatio[i] > 0.4 then
            wheelSpinVib = wheelSpinVib + math.abs(wheelSlipRatio[i])
        end

        if i <= 2 and wheelSlipRatio[i] < -0.4 then
            wheelLockVib = wheelLockVib + math.abs(wheelSlipRatio[i])
        end
    end

    -- Slip Ratio vib
    local ffbSlipRatioForce = 0
    if spinActiveK == 1 and wheelSpinVib > 0.01 then
        local slipNormalized = math.saturate((wheelSpinVib - 1) / 3)
        local slipMapped = 0.30 + (0.60 - 0.30) * slipNormalized
        local slipFreq = spinFrequencyK * slipNormalized
        spinPhase = (spinPhase + slipFreq * dt) % 1
        local slipOscillation = math.sin(spinPhase * 2 * math.pi)
        ffbSlipRatioForce = (slipOscillation * slipMapped * spinIntensityK * baseGain) / (1 + math.abs(ffbValue))
    elseif lockActiveK == 1 and wheelLockVib > 0 then
        local lockNormalized = math.saturate(wheelLockVib / 2.0)
        local maxReductionPercentage = 0.55
        local sideForceReductionMultiplier = 1.0 - (lockNormalized * maxReductionPercentage)
        ffbValue = ffbValue * sideForceReductionMultiplier
        local lockMapped = 0.4 + (0.9 - 0.4) * lockNormalized
        local lockVibrationMult = lockMapped * lockIntensityK
        local lockOscillation = math.sin(simTime * lockFrequencyK * 2 * math.pi)
        local jitter = 0.9 + math.random() * 0.2
        ffbSlipRatioForce = (lockOscillation * lockVibrationMult * jitter * baseGain) / (1 + math.abs(ffbValue))
    else
        if lateralSlipActiveK == 1 then
            local frontSlipAngle = (math.abs(wheelSlipAngle[1]) + math.abs(wheelSlipAngle[2])) / 2
            local rearSlipAngle = (math.abs(wheelSlipAngle[3]) + math.abs(wheelSlipAngle[4])) / 2
            local slipDifference = frontSlipAngle - rearSlipAngle
            local avgSlip = (frontSlipAngle + rearSlipAngle) / 2

            if avgSlip > 0.0125 then
                local slipType = "neutral"
                local slipTypeIntensity = 0
                local neutralThreshold = 0.01
                local hysteresis = 0.01

                if lastSlipType == "understeer" then
                    if slipDifference > (neutralThreshold - hysteresis) then
                        slipType = "understeer"
                        slipTypeIntensity = math.saturate((slipDifference - neutralThreshold) / 0.05)
                    elseif slipDifference < -neutralThreshold then
                        slipType = "oversteer"
                        slipTypeIntensity = math.saturate((-slipDifference - neutralThreshold) / 0.05)
                    else
                        slipType = "neutral"
                        slipTypeIntensity = 0
                    end
                elseif lastSlipType == "oversteer" then
                    if slipDifference < (-neutralThreshold + hysteresis) then
                        slipType = "oversteer"
                        slipTypeIntensity = math.saturate((-slipDifference - neutralThreshold) / 0.05)
                    elseif slipDifference > neutralThreshold then
                        slipType = "understeer"
                        slipTypeIntensity = math.saturate((slipDifference - neutralThreshold) / 0.05)
                    else
                        slipType = "neutral"
                        slipTypeIntensity = 0
                    end
                else
                    if slipDifference > neutralThreshold then
                        slipType = "understeer"
                        slipTypeIntensity = math.saturate((slipDifference - neutralThreshold) / 0.05)
                    elseif slipDifference < -neutralThreshold then
                        slipType = "oversteer"
                        slipTypeIntensity = math.saturate((-slipDifference - neutralThreshold) / 0.05)
                    else
                        slipType = "neutral"
                        slipTypeIntensity = 0
                    end
                end

                lastSlipType = slipType

                local slipNorm = math.saturate((avgSlip - 0.06) / 0.25)
                local baseIntensity
                if slipType == "understeer" then
                    baseIntensity = slipNorm * (lateralSlipIntensityK * (1 - slipTypeIntensity) + understeerIntensityK * slipTypeIntensity)
                elseif slipType == "oversteer" then
                    baseIntensity = slipNorm * (lateralSlipIntensityK * (1 - slipTypeIntensity) + oversteerIntensityK * slipTypeIntensity)
                else
                    baseIntensity = slipNorm * (lateralSlipIntensityK * 0.9)
                end

                local function convertToBaseFreq(maxFreq) return (maxFreq - 22.5) / 1.2 end
                local understeerBaseFreq = convertToBaseFreq(understeerFrequencyK)
                local oversteerBaseFreq = convertToBaseFreq(oversteerFrequencyK)
                local lateralSlipBaseFreq = convertToBaseFreq(lateralSlipFrequencyK)

                local baseFreq
                if slipType == "understeer" then
                    baseFreq = lateralSlipBaseFreq + (understeerBaseFreq - lateralSlipBaseFreq) * slipTypeIntensity
                elseif slipType == "oversteer" then
                    baseFreq = lateralSlipBaseFreq + (oversteerBaseFreq - lateralSlipBaseFreq) * slipTypeIntensity
                else
                    baseFreq = lateralSlipBaseFreq
                end

                local minSpeedThreshold = 5
                local baseMinFreq = 15
                local speedScaleFactor = 0.8
                local speedFactor = math.max(carSpeedKmh - minSpeedThreshold, 0) / 115
                local speedScaledFreq = baseMinFreq + (baseFreq * speedFactor * speedScaleFactor)
                local finalFreq = speedScaledFreq * (0.8 + slipNorm * 0.7)

                lateralSlipPhase = (lateralSlipPhase + finalFreq * dt) % 1
                local oscillation = math.sin(lateralSlipPhase * 2 * math.pi)
                local speedNorm = 0.4 + (math.saturate(carSpeedKmh / 200))
                local finalIntensity = baseIntensity * speedNorm
                ffbSlipRatioForce = ffbSlipRatioForce + (oscillation * finalIntensity * baseGain) / (1 + math.abs(ffbValue))
            end
        end
    end

    -- Gear jolt
    local ffbGearJoltForce = 0
    if joltActiveK == 1 then
        if carPh.gear ~= lastGear then
            local rpmLimiter = carData.rpmLimiter or 7000
            local rpmRatio = carPh.rpm / rpmLimiter
            local maxReduction = 0.20
            local reductionScale = 0
            if rpmRatio <= 1.0 and rpmRatio >= 0.5 then
                reductionScale = (1.0 - rpmRatio) * 2
            else
                reductionScale = 1
            end
            local shiftForceMultiplier = 1 + (maxReduction * reductionScale)
            local gearBasedForceMultiplier = 1
            if carPh.gear == 2 then
                gearBasedForceMultiplier = 1.4071
            elseif carPh.gear == 3 then
                gearBasedForceMultiplier = 1.3400
            elseif carPh.gear == 4 then
                gearBasedForceMultiplier = 1.2762
            elseif carPh.gear == 5 then
                gearBasedForceMultiplier = 1.2155
            elseif carPh.gear == 6 then
                gearBasedForceMultiplier = 1.1576
            elseif carPh.gear == 7 then
                gearBasedForceMultiplier = 1.1025
            elseif carPh.gear == 8 then
                gearBasedForceMultiplier = 1.05
            elseif carPh.gear == 9 then
                gearBasedForceMultiplier = 1.0
            elseif carPh.gear == 1 then
                gearBasedForceMultiplier = 0.3
            elseif carPh.gear == 0 then
                gearBasedForceMultiplier = 1.4071
            else
                gearBasedForceMultiplier = 1.0
            end
            shiftForceMultiplier = shiftForceMultiplier * gearBasedForceMultiplier
            gearJoltBaseForce = joltForceK * shiftForceMultiplier
            joltTime = 0
            shiftPhase = math.random(0, 1) == 0 and -1 or 1
            lastGear = carPh.gear
        elseif joltTime >= joltDurationK then
            ffbGearJoltForce = 0
        else
            local normalizedTime = joltTime / joltDurationK
            local wave = (2 * math.abs(2 * (normalizedTime % 1) - 1) - 1) * shiftPhase
            local fadeFactor = 1 - (joltTime / joltDurationK)
            fadeFactor = math.max(fadeFactor, 0)
            ffbGearJoltForce = (gearJoltBaseForce * wave * fadeFactor * baseGain) / (1 + math.abs(ffbValue))
            joltTime = joltTime + dt
        end
    end

    -- Aero Damping Effect
    local ffbAeroDamping = 0
    if aeroActiveK == 1 then
        local aeroSpeedFactor = math.smoothstep(math.saturate(carSpeedKmh / 290))
        ffbAeroDamping = aeroDampingIntensityK * math.pow(aeroSpeedFactor, 2)
    end

    local leftWheelOnDirty = dirtySurfaces[frontTyreSurface[1]]
    local rightWheelOnDirty = dirtySurfaces[frontTyreSurface[2]]
    
    local anyWheelOnDirty = leftWheelOnDirty or rightWheelOnDirty

    -- Scrub
    local ffbStationaryScrub = 0
    if stationaryScrubActiveK == 1 and carSpeedKmh < 0.4 and not anyWheelOnDirty then
        local scrubFactor = math.smoothstep(math.saturate(math.abs(steerInputSpeed) / 0.20)) 
        local loadFactor = math.min((wheelLoad[1] + wheelLoad[2]) / 2000, 1)
        local avgSlipAngle = math.max((math.abs(wheelSlipAngle[1]) + math.abs(wheelSlipAngle[2])) / 2, 0.04)
        local intensity = stationaryScrubIntensityK * loadFactor * avgSlipAngle
        local frequency = stationaryScrubFrequencyK + (stationaryScrubFrequencyK * 0.5) * avgSlipAngle
        scrubPhase = (scrubPhase + frequency * dt) % 1
        local oscillation = math.sin(scrubPhase * 3 * math.pi)
        ffbStationaryScrub = oscillation * intensity * scrubFactor * baseGain
    end

    -- Weight Transfer: uses all 4 wheel loads
    local ffbWeightTransfer = 0
    if weightTransferActiveK == 1 then
        local fL = wheelLoad[1] or 0  -- Front Left
        local fR = wheelLoad[2] or 0  -- Front Right
        local rL = wheelLoad[3] or 0  -- Rear Left
        local rR = wheelLoad[4] or 0  -- Rear Right

        local front = fL + fR
        local rear  = rL + rR
        local left  = fL + rL
        local right = fR + rR

        local longDen = math.max(front + rear, 1)
        local latDen  = math.max(left + right, 1)

        local longDiff = (front - rear) / longDen
        local latDiff  = (left - right) / latDen

        local a = weightFilter.alpha
        weightFilter.long = a * weightFilter.long + (1 - a) * longDiff
        weightFilter.lat  = a * weightFilter.lat  + (1 - a) * latDiff

        local speedFactor = 0.2 + 0.8 * math.saturate(carSpeedKmh / 140)

        local latTorque  = weightFilter.lat  * weightTransferIntensityK * 0.90 * speedFactor
        local longTorque = weightFilter.long * weightTransferIntensityK * 0.40 * speedFactor

        local antiClip = 1 / (1 + math.abs(ffbValue))

        ffbWeightTransfer = (latTorque + longTorque) * baseGain * antiClip
    end
    
    -- FfbFinal 
    local ffbFinalNoJolt =
        ffbSusp +
        ffbSlipRatioForce +
        ffbAbsForce +
        ffbTcForce +
        ffbStationaryScrub +
        ffbKerbForce +
        ffbVibrationForce +
        ffbDirtyTyreForce +
        ffbWeightTransfer +
        ffbOffroadForce

    local dirtReductionValue = 0.36 
    local leftDirtFactor = leftWheelOnDirty and 1 or 0   
    local rightDirtFactor = rightWheelOnDirty and 1 or 0 

    local averageDirtiness = (leftDirtFactor + rightDirtFactor) / 2.0

    local surfaceReductionMultiplier = 1.0 - (1.0 - dirtReductionValue) * averageDirtiness

    return ffbValue * surfaceReductionMultiplier, ffbFinalNoJolt, ffbAeroDamping, ffbGearJoltForce
end

function script.update(ffbValue, ffbDamper, steerInput, steerInputSpeed, dt)
    -- Blending
    lastInSession = currentInSession

    local currentInSession = isInActiveSession()
    if not lastInSession and currentInSession then

    end
    lastInSession = currentInSession
    
    local steerBlend = math.smoothstep(math.saturate((math.abs(steerInputSpeed) - 0.15) / 0.80))

    local processedFfbValue, ffbFinalNoJolt, ffbAeroDamping, ffbGearJoltForce = calculateFFBEffects(dt, ffbValue, steerInput, steerInputSpeed)

    local finalDamping = ffbDamper + ffbAeroDamping  
    
    if car.isConnected and ac.getSim().isInMainMenu == false and ac.getSim().isReplayActive == false then
        local dampK = ScriptSettings.dampK
        local slowSpeedMult = (0.85 - math.smoothstep(math.saturate((carSpeedKmh - 1) / 50)))
        finalDamping = finalDamping + math.abs(dampK) + slowSpeedMult * dampSlowSpeedK
    end

    local finalResult
    
    if interpolationSystem.enabled then
        local interpolatedFFB = updateInterpolation(processedFfbValue, ffbFinalNoJolt, dt)
        
        -- Blending
        if math.abs(carPh.speedKmh) < 0.1 then

            finalResult = interpolatedFFB * steerBlend + ffbGearJoltForce
        else
            finalResult = interpolatedFFB + ffbGearJoltForce
        end
    else
        local totalFFBNoJolt = processedFfbValue + ffbFinalNoJolt
        
        -- Blending 
        if math.abs(carPh.speedKmh) < 0.1 then

            finalResult = totalFFBNoJolt * steerBlend + ffbGearJoltForce
        else
            finalResult = totalFFBNoJolt + ffbGearJoltForce
        end
    end
    
    return finalResult, finalDamping
end