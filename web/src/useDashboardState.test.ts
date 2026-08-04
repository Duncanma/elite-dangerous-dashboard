import { beforeEach, describe, expect, it, vi } from "vitest";
import { act, renderHook } from "@testing-library/react";
import type { DashboardState } from "./api";
import { useDashboardState } from "./useDashboardState";

type Handler = (...args: unknown[]) => void;

interface FakeHub {
  on: (name: string, cb: Handler) => void;
  off: (name: string, cb: Handler) => void;
  onreconnected: (cb: () => void) => void;
  onreconnecting: (cb: () => void) => void;
  onclose: (cb: () => void) => void;
  fireReconnected: () => void;
  fireReconnecting: () => void;
}

let fakeHub: FakeHub;
let ensureStartedMock: ReturnType<typeof vi.fn<() => Promise<void>>>;

vi.mock("./signalr", () => ({
  getHub: () => fakeHub,
  ensureStarted: () => ensureStartedMock(),
}));

function makeFakeHub(): FakeHub {
  const handlers = new Map<string, Set<Handler>>();
  let reconnectedCb: (() => void) | null = null;
  let reconnectingCb: (() => void) | null = null;
  return {
    on: (name, cb) => {
      const set = handlers.get(name) ?? new Set<Handler>();
      set.add(cb);
      handlers.set(name, set);
    },
    off: (name, cb) => {
      handlers.get(name)?.delete(cb);
    },
    onreconnected: (cb) => {
      reconnectedCb = cb;
    },
    onreconnecting: (cb) => {
      reconnectingCb = cb;
    },
    onclose: () => {},
    fireReconnected: () => {
      if (!reconnectedCb) throw new Error("no onreconnected handler registered");
      reconnectedCb();
    },
    fireReconnecting: () => {
      if (!reconnectingCb) throw new Error("no onreconnecting handler registered");
      reconnectingCb();
    },
  };
}

function makeState(name: string): DashboardState {
  return { name, lastUpdatedUtc: "2026-08-03T00:00:00.000Z" } as unknown as DashboardState;
}

const fetchMock = vi.fn();
vi.stubGlobal("fetch", fetchMock);

function respondWith(state: DashboardState) {
  fetchMock.mockResolvedValue({ json: () => Promise.resolve(state) });
}

describe("useDashboardState", () => {
  beforeEach(() => {
    fakeHub = makeFakeHub();
    ensureStartedMock = vi.fn<() => Promise<void>>().mockResolvedValue(undefined);
    fetchMock.mockReset();
  });

  it("shows disconnected while the connection is reconnecting", async () => {
    respondWith(makeState("initial"));
    const { result } = renderHook(() => useDashboardState());
    await act(async () => {});
    expect(result.current.connected).toBe(true);

    act(() => fakeHub.fireReconnecting());
    expect(result.current.connected).toBe(false);
  });

  it("refetches state after a reconnect, since missed pushes are never re-sent", async () => {
    respondWith(makeState("before-drop"));
    const { result } = renderHook(() => useDashboardState());
    await act(async () => {});
    expect(result.current.state?.name).toBe("before-drop");
    expect(fetchMock).toHaveBeenCalledTimes(1);

    respondWith(makeState("after-reconnect"));
    act(() => fakeHub.fireReconnected());
    await act(async () => {});

    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(result.current.state?.name).toBe("after-reconnect");
    expect(result.current.connected).toBe(true);
  });
});
