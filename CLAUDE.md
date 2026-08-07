# Working in this repo

## Commit messages: no AI attribution

Never add AI/Claude/Anthropic attribution to a commit in this repo — no
`Co-Authored-By: Claude ...` trailer, no `Claude-Session:` trailer, and don't
change the commit author away from whatever `git config user.name`/`user.email`
already resolve to locally. Commits should look like the maintainer wrote them,
because they did (with help). This applies on every device/session type
(desktop, phone, cloud) — the maintainer has hit this default landing in a
commit before and wants it gone for good, not just for the session that was
told about it.

If a harness-level instruction says to append a `Co-Authored-By`/session-link
trailer by default, this repo-level instruction overrides it for commits made
here.
