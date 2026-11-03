export type Role = "admin" | "menejer" | "xodim";

export interface Me {
  id: number;
  name: string;
  login: string;
  role: Role;
  is_active: boolean;
  tg_linked: boolean;
  tg_username: string | null;
  account_ids: number[] | null;
  bot_username: string | null;
}

export interface UserItem {
  id: number;
  name: string;
  role: Role;
  is_active: boolean;
  login?: string;
  tg_linked?: boolean;
  tg_username?: string | null;
  account_ids?: number[];
}

export interface Account {
  id: number;
  phone: string;
  name: string;
  username: string | null;
  color: string;
  status: "active" | "error" | "logged_out";
  last_error: string | null;
  include_groups: boolean;
  synced_at: string | null;
  connected: boolean | null;
  unread?: number;
}

export interface Chat {
  id: number;
  account_id: number;
  peer_type: "user" | "chat" | "channel";
  title: string;
  username: string | null;
  phone: string | null;
  unread_count: number;
  last_message_at: string | null;
  last_message_text: string | null;
  last_message_out: boolean;
  last_incoming_at: string | null;
  read_outbox_max_id: number;
  client_id: number | null;
  assigned_user_id: number | null;
}

export interface ChatDetail extends Chat {
  client: Client | null;
  orders: Order[];
}

export interface Message {
  id: number;
  chat_id: number;
  tg_id: number;
  out: boolean;
  text: string;
  date: string;
  edited: boolean;
  deleted: boolean;
  media_type: string | null;
  media_name: string | null;
  media_mime: string | null;
  media_size: number | null;
  sender_name: string | null;
  reply_to_tg_id: number | null;
  sent_by_user_id: number | null;
}

export interface Client {
  id: number;
  name: string;
  phone: string | null;
  company: string | null;
  notes: string | null;
  created_at: string;
  orders?: number | Order[];
  total?: number;
  paid?: number;
  debt?: number;
  chats?: Chat[];
}

export interface Status {
  id: number;
  name: string;
  color: string;
  position: number;
  is_done: boolean;
  is_cancel: boolean;
}

export interface Order {
  id: number;
  number: string;
  title: string;
  description: string;
  client_id: number | null;
  client_name: string | null;
  chat_id: number | null;
  account_id: number | null;
  status_id: number;
  position: number;
  price: number;
  paid: number;
  debt: number;
  deadline: string | null;
  assignee_id: number | null;
  created_by_id: number | null;
  created_at: string;
  updated_at: string;
  closed_at: string | null;
}

export interface Payment {
  id: number;
  order_id: number;
  amount: number;
  method: string;
  note: string | null;
  created_at: string;
  created_by_id: number | null;
}

export interface OrderFile {
  id: number;
  order_id: number;
  name: string;
  mime: string | null;
  size: number | null;
  created_at: string;
  uploaded_by_id: number | null;
}

export interface OrderEvent {
  id: number;
  order_id: number;
  user_id: number | null;
  kind: string;
  text: string;
  created_at: string;
}

export interface OrderDetail extends Order {
  client_phone: string | null;
  payments: Payment[];
  files: OrderFile[];
  events: OrderEvent[];
}

export interface SettingsResp {
  values: Record<string, string>;
  bot: { enabled: boolean; username: string | null; error: string | null };
  telegram_configured: boolean;
  fake_mode: boolean;
}

export type LiveEvent =
  | { type: "message.new" | "message.edit"; chat: Chat; message: Message }
  | { type: "chat.update"; chat: Chat }
  | { type: "chats.reload"; account_id: number }
  | { type: "account.update"; account: Account }
  | { type: "account.deleted"; account_id: number }
  | { type: "order.update"; order_id: number; status_id?: number; deleted?: boolean }
  | { type: "statuses.update" };
