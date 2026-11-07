type Field = { label: string } & (
  { value: string } | { option: string } | { checked: boolean }
);
type Fixture = {
  kind: "accounts" | "destinations" | "schedules" | "rules" | "settings";
  name: string;
  fields: Field[];
};
export const configurationFixtures: Fixture[] = [
  {
    kind: "accounts",
    name: "Online",
    fields: [
      { label: "API 编号", value: "12345" },
      { label: "API 哈希", value: "0123456789abcdef0123456789abcdef" },
      { label: "手机号码", value: "+886912345677" },
    ],
  },
  {
    kind: "destinations",
    name: "UI Bark",
    fields: [
      { label: "设备密钥", value: "fake-ui-device" },
      { label: "加密通知", checked: true },
      { label: "加密密钥", value: "0123456789abcdef" },
      { label: "初始向量", value: "0123456789abcdef" },
    ],
  },
  {
    kind: "schedules",
    name: "UI schedule",
    fields: [
      { label: "Telegram 账号", option: "Online updated" },
      { label: "接收对象", value: "@test_bot" },
      { label: "消息正文", value: "Disabled browser fixture" },
      { label: "发送频率", option: "每年" },
      { label: "当地时间", value: "00:00:00" },
      { label: "每月日期", value: "1" },
      { label: "月份", value: "1" },
    ],
  },
  {
    kind: "rules",
    name: "UI rule",
    fields: [
      { label: "Bark 推送目标", option: "UI Bark updated" },
      { label: "触发事件", option: "离线" },
      { label: "监控对象", option: "QA renamed user" },
      { label: "标题模板", value: "{NAME} offline" },
      { label: "正文模板", value: "{TIME_OF_ONLINE}" },
      { label: "副标题模板", value: "presence" },
      { label: "分组模板", value: "copilot" },
      {
        label: "其他 Bark 参数",
        value: '{"level":"timeSensitive","volume":5}',
      },
    ],
  },
  { kind: "settings", name: "UI system", fields: [] },
];
