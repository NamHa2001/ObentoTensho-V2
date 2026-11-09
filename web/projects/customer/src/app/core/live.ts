import type { HubConnection } from '@microsoft/signalr';

export interface LiveOptions {
  url: () => Promise<string>;
  events: string[];
  onEvent: (name: string, payload: any) => void;
  // Gọi sau khi nối được: đăng ký theo dõi (nếu có) và đồng bộ lại dữ liệu đã lỡ trong lúc mất kết nối (RT-06)
  onOpen?: (connection: HubConnection, reconnected: boolean) => Promise<unknown>;
  onState?: (connected: boolean) => void;
}

const MAX_DELAY = 30000;

// RT-09: vé kết nối dùng một lần nên mỗi lần nối lại phải xin vé mới, không dùng tự-nối-lại của SignalR
export class LiveHub {
  private connection: HubConnection | null = null;
  private stopped = false;
  private attempts = 0;
  private everOpen = false;
  private timer: ReturnType<typeof setTimeout> | null = null;
  private run = 0;

  constructor(private readonly options: LiveOptions) {}

  async start(): Promise<void> {
    const run = ++this.run;
    try {
      const { HubConnectionBuilder, HttpTransportType, LogLevel } = await import('@microsoft/signalr');
      const url = await this.options.url();
      if (this.stopped || run !== this.run) {
        return;
      }

      const connection = new HubConnectionBuilder().withUrl(url, { skipNegotiation: true, transport: HttpTransportType.WebSockets }).configureLogging(LogLevel.None).build();
      for (const name of this.options.events) {
        connection.on(name, (payload: unknown) => this.options.onEvent(name, payload));
      }

      // Chỉ kết nối đang dùng mới được kích hoạt nối lại; kết nối đã bị thay hoặc dừng thì bỏ qua
      connection.onclose(() => {
        if (this.connection === connection) {
          this.connection = null;
          this.retry();
        }
      });
      this.connection = connection;
      await connection.start();
      if (this.stopped || run !== this.run) {
        void connection.stop();
        return;
      }

      await this.options.onOpen?.(connection, this.everOpen);
      this.everOpen = true;
      this.attempts = 0;
      this.options.onState?.(true);
    } catch {
      const failed = this.connection;
      this.connection = null;
      void failed?.stop();
      this.retry();
    }
  }

  stop(): void {
    this.stopped = true;
    this.run += 1;
    if (this.timer) {
      clearTimeout(this.timer);
    }

    const connection = this.connection;
    this.connection = null;
    void connection?.stop();
  }

  private retry(): void {
    this.options.onState?.(false);
    if (this.stopped) {
      return;
    }

    this.attempts += 1;
    this.timer = setTimeout(() => void this.start(), Math.min(MAX_DELAY, 1000 * 2 ** this.attempts));
  }
}
