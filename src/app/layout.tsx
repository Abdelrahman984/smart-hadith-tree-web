import type { Metadata } from "next";
import { Noto_Sans_Arabic, Inter } from "next/font/google";
import { QueryProvider } from "@/components/QueryProvider";
import "./globals.css";

const notoSansArabic = Noto_Sans_Arabic({
  variable: "--font-noto-sans-arabic",
  subsets: ["arabic"],
  weight: ["400", "500", "600", "700"],
});

const inter = Inter({
  variable: "--font-inter",
  subsets: ["latin"],
});

export const metadata: Metadata = {
  title: "Smart Hadith Tree | شجرة الأسانيد الذكية",
  description: "A platform for digitizing and visualizing Hadith narrator chains (Isnad)",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="ar" dir="rtl" className={`${notoSansArabic.variable} ${inter.variable} font-arabic h-full antialiased`}>
      <body suppressHydrationWarning className="min-h-full flex flex-col bg-slate-50 text-slate-900">
        <QueryProvider>
          {children}
        </QueryProvider>
      </body>
    </html>
  );
}
