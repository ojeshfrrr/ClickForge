# ClickForge v1.6.2

The Windows application and updater now use compressed single-file bundles to
reduce the space occupied by their executables while remaining self-contained.
The existing clicking, target validation, shortcuts and update safeguards are
unchanged. The Setup download may be larger than v1.6.1 because it compresses
an already compressed payload; startup may also be slower. The portable ZIP
remains available alongside Setup.

This release does not move the in-app updater from GitHub to Portways. The
Portways website's download catalogue must be updated separately to offer this
version. The executables are unsigned; SHA-256 checks integrity, not publisher
identity. See the workflow results for the checks performed for this tag.
