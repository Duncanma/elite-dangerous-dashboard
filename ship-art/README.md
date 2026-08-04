# Ship art backup

This folder is a **backup copy** of your personal ship art. Everything in it
is git-ignored (except this README) — the images are personal to your install
and possibly not freely licensed, so they must never be committed. Do not
`git add -f` anything here.

Layout mirrors the live folder the app actually serves from
(`%LOCALAPPDATA%\EliteDashboard\ship-art\`):

- `by-type\{Ship type name}.{webp|png|jpg|jpeg}` — generic art for any ship of that type
- `by-ship\{Your ship's name}.{webp|png|jpg|jpeg}` — a specific hull

How it works: `deploy\publish.ps1` copies the contents of this folder into
`%LOCALAPPDATA%\EliteDashboard\ship-art\` at the end of every publish
(additive only — it never deletes files that exist only in the live folder).
So if the app-data folder is ever wiped, the next publish restores your art.

The live folder is still the place to drop new art for instant effect — copy
it here afterwards if you want it backed up. Note that `git clean -fdx`
deletes ignored files, including everything here.
