import { useTranslations } from 'next-intl';

export default function HomePage() {
  return <Welcome />;
}

function Welcome() {
  const t = useTranslations('home');
  return (
    <main className="flex min-h-screen flex-col items-center justify-center gap-2 p-8">
      <h1 className="text-3xl font-semibold">{t('title')}</h1>
      <p className="text-muted-foreground">{t('subtitle')}</p>
    </main>
  );
}
