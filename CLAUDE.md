# CLAUDE.md - AI Assistant Guide for FFB Repository

> **Last Updated**: 2026-01-22
> **Repository**: tibalotte/ffb
> **Status**: Initial repository setup

---

## 📋 Table of Contents

1. [Repository Overview](#repository-overview)
2. [Codebase Structure](#codebase-structure)
3. [Development Workflow](#development-workflow)
4. [Git Conventions](#git-conventions)
5. [Code Quality Standards](#code-quality-standards)
6. [Testing Guidelines](#testing-guidelines)
7. [Documentation Standards](#documentation-standards)
8. [Common Patterns](#common-patterns)
9. [AI Assistant Guidelines](#ai-assistant-guidelines)
10. [Troubleshooting](#troubleshooting)

---

## 🎯 Repository Overview

### Current State
This repository is currently in its initial setup phase. As the codebase develops, this section should be updated with:
- Project purpose and goals
- Target audience/users
- Key features and functionality
- Technology stack and dependencies

### Quick Start
```bash
# Clone the repository
git clone http://127.0.0.1:54399/git/tibalotte/ffb
cd ffb

# TODO: Add setup instructions as project develops
# Example: npm install, pip install -r requirements.txt, etc.
```

---

## 📁 Codebase Structure

### Recommended Directory Layout

As this project develops, maintain a clear and logical directory structure. Common patterns include:

```
/
├── src/                    # Source code
│   ├── components/         # Reusable components
│   ├── services/           # Business logic/services
│   ├── utils/              # Utility functions
│   └── config/             # Configuration files
├── tests/                  # Test files
├── docs/                   # Documentation
├── scripts/                # Build/deployment scripts
├── .github/                # GitHub workflows and templates
├── CLAUDE.md              # This file
├── README.md              # User-facing documentation
└── package.json           # Dependencies (adjust for language)
```

### Key Files to Maintain
- **README.md**: User-facing documentation, setup instructions
- **CLAUDE.md**: This file - AI assistant guidelines
- **CHANGELOG.md**: Track notable changes
- **CONTRIBUTING.md**: Contribution guidelines (if applicable)
- **.gitignore**: Ignore patterns for version control
- **LICENSE**: Project license

---

## 🔄 Development Workflow

### Branch Strategy

**Main Branches**:
- `main` or `master`: Production-ready code
- `develop`: Integration branch for features (if using git-flow)

**Feature Branches**:
- Naming: `feature/description` or `claude/claude-md-mkpoi6nj4oderjob-rpR7b`
- Create from: `main` or `develop`
- Merge into: `main` or `develop` via Pull Request

**Other Branch Types**:
- `bugfix/description`: Bug fixes
- `hotfix/description`: Emergency production fixes
- `refactor/description`: Code refactoring
- `docs/description`: Documentation updates

### Development Process

1. **Start a Task**
   - Create or checkout the appropriate branch
   - Pull latest changes: `git pull origin <branch>`
   - Understand requirements completely before coding

2. **Make Changes**
   - Read existing code before modifying
   - Follow existing patterns and conventions
   - Write tests for new functionality
   - Update documentation as needed

3. **Commit Changes**
   - Stage relevant files: `git add <files>`
   - Write clear commit messages (see Git Conventions)
   - Run tests before committing
   - Commit: `git commit -m "message"`

4. **Push Changes**
   - Push to remote: `git push -u origin <branch>`
   - Retry with exponential backoff if network errors occur

5. **Create Pull Request**
   - Provide clear title and description
   - Reference related issues
   - Include test plan
   - Request review if applicable

---

## 📝 Git Conventions

### Commit Messages

Follow the Conventional Commits specification:

```
<type>(<scope>): <subject>

<body>

<footer>
```

**Types**:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation changes
- `refactor`: Code refactoring
- `test`: Adding/updating tests
- `chore`: Maintenance tasks
- `perf`: Performance improvements
- `style`: Code style changes (formatting, etc.)

**Examples**:
```bash
feat(auth): add JWT authentication
fix(api): handle null response in getUserData
docs(readme): update installation instructions
refactor(utils): simplify date formatting logic
test(services): add unit tests for payment processing
```

### Commit Best Practices

1. **Atomic Commits**: Each commit should represent a single logical change
2. **Clear Messages**: Explain WHY the change was made, not just WHAT
3. **Reference Issues**: Include issue numbers when applicable (e.g., `fixes #123`)
4. **Avoid Mass Commits**: Don't bundle unrelated changes
5. **No Secrets**: Never commit sensitive data, credentials, or API keys

### Git Operations Safety

**Always Safe**:
- `git status`, `git log`, `git diff`
- `git fetch`, `git pull`
- `git add`, `git commit`
- `git push` (to feature branches)

**Use with Caution**:
- `git push --force`: Never to main/master, only to feature branches if necessary
- `git commit --amend`: Only for unpushed commits
- `git reset --hard`: Can lose uncommitted work
- `git rebase`: Never on shared branches

**Never Do**:
- Force push to main/master branches
- Skip git hooks (`--no-verify`)
- Commit large binary files without LFS
- Rewrite public history

---

## 🎨 Code Quality Standards

### General Principles

1. **Readability First**: Code is read more than written
2. **Keep It Simple**: Avoid over-engineering
3. **DRY (Don't Repeat Yourself)**: Extract common logic
4. **YAGNI (You Aren't Gonna Need It)**: Don't add features you don't need now
5. **Separation of Concerns**: Each module should have a single responsibility

### Code Style

- Follow language-specific style guides (PEP 8 for Python, Airbnb for JavaScript, etc.)
- Use consistent indentation (spaces vs tabs - follow project convention)
- Meaningful variable and function names
- Keep functions small and focused (single responsibility)
- Maximum line length: 80-120 characters (follow project standard)

### Comments and Documentation

**When to Comment**:
- Complex algorithms that aren't self-evident
- Business logic that requires context
- Workarounds for bugs in dependencies
- Public API documentation

**When NOT to Comment**:
- Obvious code that explains itself
- Redundant information
- Commented-out code (use git history instead)
- TODO comments for quick fixes (create issues instead)

### Security Considerations

**Always Avoid**:
- SQL Injection: Use parameterized queries
- XSS (Cross-Site Scripting): Sanitize user input
- Command Injection: Validate and escape shell commands
- Hardcoded Secrets: Use environment variables
- Insecure Dependencies: Keep packages updated

**Security Checklist**:
- [ ] User input is validated and sanitized
- [ ] Authentication and authorization are properly implemented
- [ ] Sensitive data is encrypted
- [ ] HTTPS is used for all external communications
- [ ] Error messages don't leak sensitive information
- [ ] Rate limiting is implemented for public APIs

---

## 🧪 Testing Guidelines

### Test Structure

```
tests/
├── unit/              # Unit tests - test individual functions/classes
├── integration/       # Integration tests - test component interactions
├── e2e/              # End-to-end tests - test full workflows
└── fixtures/         # Test data and mocks
```

### Testing Best Practices

1. **Write Tests First** (TDD): Consider test-driven development
2. **Test Coverage**: Aim for >80% coverage, but focus on critical paths
3. **Test Naming**: Clear, descriptive test names that explain what's being tested
4. **Arrange-Act-Assert**: Structure tests in three phases
5. **Isolation**: Tests should be independent and not rely on order
6. **Mock External Dependencies**: Use mocks for APIs, databases, file systems

### What to Test

**Priority 1 - Must Test**:
- Business logic and core functionality
- Edge cases and error handling
- Security-critical code
- Public APIs and interfaces

**Priority 2 - Should Test**:
- Utility functions
- Data transformations
- Integration points

**Priority 3 - Nice to Test**:
- UI components
- Simple getters/setters
- Configuration

### Running Tests

```bash
# TODO: Update with actual commands as project develops
# npm test
# pytest
# go test ./...
```

---

## 📚 Documentation Standards

### Code Documentation

1. **Public APIs**: Document all public functions, classes, and modules
2. **Parameters**: Describe each parameter type and purpose
3. **Return Values**: Document what the function returns
4. **Exceptions**: List exceptions that can be thrown
5. **Examples**: Provide usage examples for complex APIs

### README.md Structure

```markdown
# Project Name

Brief description

## Features
- Feature 1
- Feature 2

## Installation
Step-by-step setup

## Usage
Examples and common use cases

## Configuration
Environment variables and settings

## Contributing
How to contribute

## License
License information
```

### Inline Documentation

```python
# Python example
def calculate_total(items: list[dict], tax_rate: float) -> float:
    """
    Calculate the total price including tax.

    Args:
        items: List of items with 'price' keys
        tax_rate: Tax rate as decimal (e.g., 0.08 for 8%)

    Returns:
        Total price including tax

    Raises:
        ValueError: If tax_rate is negative
    """
    # Implementation
```

---

## 🔧 Common Patterns

### Error Handling

**Pattern**: Fail fast, handle gracefully

```javascript
// Example: Validate input early
function processUser(user) {
    if (!user || !user.id) {
        throw new Error('Invalid user: missing id');
    }

    try {
        // Process user
        return saveUser(user);
    } catch (error) {
        logger.error('Failed to process user', { user, error });
        throw new Error('User processing failed');
    }
}
```

### Configuration Management

**Pattern**: Use environment variables for configuration

```bash
# .env.example
DATABASE_URL=postgresql://localhost/mydb
API_KEY=your_api_key_here
LOG_LEVEL=info
```

### Dependency Injection

**Pattern**: Inject dependencies rather than hardcoding

```python
# Good: Dependency injection
class UserService:
    def __init__(self, database, logger):
        self.db = database
        self.logger = logger

# Bad: Hardcoded dependencies
class UserService:
    def __init__(self):
        self.db = Database()  # Hardcoded!
        self.logger = Logger()  # Hardcoded!
```

---

## 🤖 AI Assistant Guidelines

### When Working on This Codebase

1. **Always Read Before Modifying**
   - Never modify code you haven't read
   - Understand the context and existing patterns
   - Check for related code that might be affected

2. **Use the TodoWrite Tool**
   - Create todos for multi-step tasks
   - Mark tasks as in_progress when starting
   - Mark tasks as completed immediately upon finishing
   - Keep exactly ONE task in_progress at a time

3. **Ask Questions**
   - Use AskUserQuestion when requirements are unclear
   - Clarify before making architectural decisions
   - Confirm destructive operations

4. **Follow Existing Patterns**
   - Match the style and structure of existing code
   - Don't introduce new patterns without good reason
   - Maintain consistency across the codebase

5. **Security First**
   - Never commit secrets or credentials
   - Validate and sanitize all user input
   - Use parameterized queries for databases
   - Avoid common vulnerabilities (OWASP Top 10)

6. **Keep Changes Focused**
   - Only make changes that are directly requested
   - Don't refactor code unless asked
   - Don't add extra features "for the future"
   - Avoid over-engineering solutions

7. **Test Your Changes**
   - Run existing tests after modifications
   - Add tests for new functionality
   - Verify changes work as expected

8. **Document Changes**
   - Update relevant documentation
   - Add comments for complex logic
   - Keep README.md and this file current

### Git Workflow for AI Assistants

1. **Check Current Branch**
   ```bash
   git status
   git branch
   ```

2. **Fetch Latest Changes**
   ```bash
   git fetch origin <branch>
   ```

3. **Make Changes**
   - Read files before modifying
   - Use Edit tool for existing files
   - Use Write only for new files

4. **Review Changes**
   ```bash
   git status
   git diff
   ```

5. **Commit Changes**
   ```bash
   git add <files>
   git commit -m "type(scope): clear description"
   ```

6. **Push to Remote**
   ```bash
   git push -u origin <branch>
   ```
   - Retry up to 4 times with exponential backoff (2s, 4s, 8s, 16s) on network errors
   - Branch must start with 'claude/' and match session ID

7. **Create Pull Request**
   ```bash
   gh pr create --title "Clear title" --body "Description with test plan"
   ```

### Common Mistakes to Avoid

❌ **Don't**:
- Modify code without reading it first
- Create files unnecessarily (prefer editing existing files)
- Use bash for file operations (use Read, Edit, Write tools)
- Add emojis unless explicitly requested
- Include time estimates in plans
- Batch multiple todos as completed
- Skip testing after making changes
- Force push to main/master branches

✅ **Do**:
- Read all relevant code before making changes
- Use specialized tools (Read, Edit, Grep, Glob)
- Follow existing code patterns and style
- Write clear commit messages
- Update documentation alongside code
- Ask questions when uncertain
- Keep changes minimal and focused
- Test changes before committing

---

## 🔍 Troubleshooting

### Common Issues

**Issue**: Tests are failing after changes
- Run tests locally before committing
- Check if new dependencies are missing
- Verify environment variables are set
- Review recent changes with `git diff`

**Issue**: Merge conflicts
- Fetch latest changes: `git fetch origin <branch>`
- Review conflicting files carefully
- Test after resolving conflicts
- Ask for help if conflicts are complex

**Issue**: Git push fails with 403
- Ensure branch name starts with 'claude/'
- Verify branch name matches session ID
- Check network connectivity
- Retry with exponential backoff

**Issue**: Can't find specific code
- Use Grep tool to search code content
- Use Glob tool to find files by pattern
- Use Task tool with Explore agent for broad searches
- Check related files and imports

### Debug Checklist

- [ ] Did you read the existing code?
- [ ] Are all dependencies installed?
- [ ] Are environment variables configured?
- [ ] Did you run tests?
- [ ] Did you check git status?
- [ ] Are there any uncommitted changes?
- [ ] Did you review error logs?
- [ ] Did you check documentation?

---

## 📈 Project Status

### To Be Determined

As this project develops, update this section with:

- **Technology Stack**: Languages, frameworks, libraries
- **Architecture**: System design, data flow, key components
- **External Services**: APIs, databases, third-party integrations
- **Deployment**: Hosting, CI/CD, environments
- **Performance**: Benchmarks, optimization strategies
- **Roadmap**: Planned features and improvements

---

## 📞 Support

### Getting Help

- Check README.md for user documentation
- Review this file for development guidelines
- Search existing issues and pull requests
- Check inline code documentation
- Review test cases for usage examples

### Contributing

When contributing to this repository:

1. Fork the repository (if applicable)
2. Create a feature branch
3. Make your changes following these guidelines
4. Write/update tests
5. Update documentation
6. Submit a pull request

---

## 📝 Maintaining This Document

**When to Update CLAUDE.md**:
- Major architectural changes
- New development patterns established
- Technology stack changes
- Workflow improvements
- Common issues discovered
- New conventions adopted

**Who Should Update**:
- Any developer or AI assistant working on the codebase
- Keep updates in sync with code changes
- Update the "Last Updated" date at the top

---

**Note**: This document is a living guide. As the FFB repository evolves, so should this documentation. Keep it current, keep it useful, and keep it concise.
