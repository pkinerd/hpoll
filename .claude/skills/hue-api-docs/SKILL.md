---
name: hue-api-docs
description: Securely extract Hue API docs from the AES-encrypted private archive into a temporary directory outside the git tree. Use when you need to reference the Hue API documentation (e.g., for implementing API calls, understanding endpoints, or answering questions about the Hue API).
user_invocable: true
---

# Hue API Docs

## ⚠️ CRITICAL — NEVER CHECK IN EXTRACTED DOCUMENTS

The extracted API documentation is proprietary and copyright-protected. **You MUST NEVER:**

- Stage, commit, or push any extracted file
- Copy extracted content into tracked files (source code, markdown, configs, etc.)
- Create new files in the repository containing extracted documentation content
- Include verbatim API specification text in commit messages or PR descriptions

The extracted documents exist **only in a temporary directory outside the git tree** and are for **in-session reference only**. Treat them as ephemeral read-only context.

---

## Instructions

### Step 1: Ensure pyzipper is available

The archive uses AES encryption, which requires `pyzipper`. Check if it is installed, and install it if missing:

```bash
python3 -c "import pyzipper" 2>/dev/null || pip install pyzipper
```

### Step 2: Ask the user for the archive password

Use `AskUserQuestion` to securely collect the password. Do NOT guess, hardcode, or reuse a password from a previous session.

Prompt the user with a question like:

> "What is the password for the encrypted Hue API archive (`docs/private/api_specs_copyright.zip`)?"

Use a free-text question (provide two options such as "Enter password" and "Skip / I don't have it"). If the user selects skip or declines, abort gracefully and inform them the API docs are unavailable for this session.

### Step 3: Extract the archive to a temporary directory

Create a temporary directory **outside** the git working tree and extract the archive there using `pyzipper`:

```bash
python3 -c "
import pyzipper, tempfile, os

extract_dir = tempfile.mkdtemp(prefix='hue_api_docs_')
zip_path = os.path.join(os.environ.get('REPO_ROOT', '.'), 'docs/private/api_specs_copyright.zip')

with pyzipper.AESZipFile(zip_path, 'r') as zf:
    zf.setpassword(b'USER_PASSWORD_HERE')
    zf.extractall(extract_dir)

print(extract_dir)
"
```

Replace `USER_PASSWORD_HERE` with the password collected in Step 2. Use the actual repository root path for `zip_path` (i.e., `/home/user/hpoll/docs/private/api_specs_copyright.zip` or determine it via `git rev-parse --show-toplevel`).

**Record the printed `extract_dir` path** — you will need it for subsequent steps and to tell the user where the docs are.

If extraction fails with a bad password error, inform the user and offer to retry (go back to Step 2). Do NOT retry silently.

### Step 4: List extracted contents and locate the README

List the extracted directory contents:

```bash
find <extract_dir> -type f
```

Locate the README markdown file (e.g., `README.md`, `readme.md`, or similar).

### Step 5: Read the README into context

Use the `Read` tool to read the README markdown file from the temporary directory. This README explains the structure of the extracted docs and how to find specific API specifications.

**Only read the README at this stage.** Do NOT read other extracted files unless the user or a subsequent task specifically requests them. This minimizes context usage and keeps sensitive material scoped tightly.

### Step 6: Report to the user

Tell the user:

1. That the docs were extracted successfully
2. The temporary directory path where they reside
3. A summary of what the README says (structure, available docs)
4. That the documents will be cleaned up when the temp directory is removed (e.g., on system reboot or session end)
5. Remind them (and yourself): **these files must never be committed to the repository**

## Example Flow

```
User: /hue-api-docs

Claude:
1. Checks pyzipper is installed (installs if needed)
2. Asks: "What is the password for the encrypted Hue API archive?"
3. User provides password
4. Extracts to /tmp/hue_api_docs_a1b2c3/
5. Lists files, finds README.md
6. Reads README.md into context
7. Reports:
   "Extracted Hue API docs to /tmp/hue_api_docs_a1b2c3/.
    The archive contains: [summary from README]
    ⚠️ These files are temporary and must never be checked into git."
```

## Subsequent Usage

After extraction, if you need to reference a specific API doc file during this session:

1. Use the `Read` tool with the full path under the temporary directory
2. Reference the README for guidance on which file contains what
3. **Never** copy doc content into repository files — paraphrase or reference by concept only

## Important Notes

- The temporary directory is **outside the git tree** — it will not appear in `git status`
- If the session ends, the extracted docs are gone — the user must re-run this skill in a new session
- The archive password should never be stored, logged, or written to any file
- These documents are copyright-protected — do not reproduce their content in committed code or documentation
