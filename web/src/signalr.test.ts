import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

type StartFn = () => Promise<void>;

interface FakeConnection {
  state: string;
  start: ReturnType<typeof vi.fn<StartFn>>;
  onclose: (cb: () => void) => void;
  simulateClose: () => void;
}

let fake: FakeConnection;

vi.mock("@microsoft/signalr", () => {
  class HubConnectionBuilder {
    withUrl() {
      return this;
    }
    withAutomaticReconnect() {
      return this;
    }
    build() {
      return fake;
    }
  }
  return {
    HubConnectionBuilder,
    HubConnectionState: { Disconnected: "Disconnected", Connected: "Connected" },
  };
});

function makeFakeConnection(): FakeConnection {
  let closeCb: (() => void) | null = null;
  return {
    state: "Disconnected",
    start: vi.fn<StartFn>(),
    onclose: (cb) => {
      closeCb = cb;
    },
    simulateClose: () => closeCb?.(),
  };
}

describe("ensureStarted", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.resetModules();
    fake = makeFakeConnection();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("resolves immediately when already connected", async () => {
    fake.state = "Connected";
    const { ensureStarted } = await import("./signalr");

    await expect(ensureStarted()).resolves.toBeUndefined();
    expect(fake.start).not.toHaveBeenCalled();
  });

  it("keeps retrying after the initial start fails, until the backend comes up", async () => {
    fake.start
      .mockRejectedValueOnce(new Error("backend down"))
      .mockImplementation(async () => {
        fake.state = "Connected";
      });

    const { ensureStarted } = await import("./signalr");
    let settled = false;
    const promise = ensureStarted().then(() => {
      settled = true;
    });

    await vi.advanceTimersByTimeAsync(0);
    expect(settled).toBe(false);

    await vi.advanceTimersByTimeAsync(30_000);
    await promise;
    expect(settled).toBe(true);
    expect(fake.start.mock.calls.length).toBeGreaterThanOrEqual(2);
  });

  it("starts again after the connection closes (automatic reconnect gave up)", async () => {
    fake.start.mockImplementation(async () => {
      fake.state = "Connected";
    });
    const { ensureStarted } = await import("./signalr");
    await ensureStarted();
    expect(fake.start).toHaveBeenCalledTimes(1);

    // The connection drops and SignalR's automatic reconnect exhausts its own
    // retries, then closes - the app should keep trying rather than die here.
    fake.state = "Disconnected";
    fake.start
      .mockRejectedValueOnce(new Error("still down"))
      .mockImplementation(async () => {
        fake.state = "Connected";
      });
    fake.simulateClose();

    await vi.advanceTimersByTimeAsync(60_000);
    expect(fake.start.mock.calls.length).toBeGreaterThanOrEqual(3);
  });
});
