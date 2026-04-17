# LedgeRPG

Two projects share this repo.

## Layout

```
LedgeRPG/              repo root
├── server/            Track A — Python paper server (active)
│   ├── ledgerpg/      package
│   ├── tests/
│   ├── scripts/
│   └── pyproject.toml
├── LedgeRPG/          Track B — Unity 6 WebGPU client (post-paper)
├── LICENSE            GPL-3.0-or-later
└── README.md
```

The Python package and the Unity project folder are split because Windows NTFS is
case-insensitive by default; `ledgerpg/` at the root would collide with `LedgeRPG/`.

## Track A — Python paper server (active, deadline 2026-04-20)

Headless game server consumed by MAGUS+HERMES for the AGI 2026 paper. Wire contract:
`../HERMES/docs/ledgerpg-server-mvp.md`.

```bash
cd server
pip install -e ".[dev]"
python -m ledgerpg.server --port 8765 --seed 42
# In another shell:
python scripts/reference_client.py --port 8765 --seed 42
```

Tests:

```bash
cd server
pytest
```

## Track B — Unity client (post-paper)

Unity 6 WebGPU project scaffolded under `LedgeRPG/`. Not active until after the paper.
Consumes `LedgeBoardGame`, `LedgeTCG`, and `MagiUnityTools` via `MagiUnityDependencyManager`.

## License

GPL-3.0-or-later (matches MAGUS and HERMES). See `LICENSE`.
