import "@primer/primitives/dist/css/base/size/size.css";
import "@primer/primitives/dist/css/base/typography/typography.css";
import "@primer/primitives/dist/css/functional/size/size.css";
import "@primer/primitives/dist/css/functional/size/border.css";
import "@primer/primitives/dist/css/functional/size/radius.css";
import "@primer/primitives/dist/css/functional/typography/typography.css";
import "@primer/primitives/dist/css/functional/themes/light.css";
import "@primer/primitives/dist/css/functional/themes/dark.css";
import { Providers } from "@/components/Providers";
import css from "./Root.module.css";
export const metadata = { title: "TelegramCopilot" };
export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html
      lang="zh-CN"
      data-color-mode="auto"
      data-light-theme="light"
      data-dark-theme="dark"
    >
      <body className={css.body}>
        <Providers>{children}</Providers>
      </body>
    </html>
  );
}
