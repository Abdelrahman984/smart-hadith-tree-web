import { test, expect, type Page } from '@playwright/test';
import { mockApiAvailable, MOCK_API_SKIP_REASON } from './fixtures/mockApi';

test.beforeAll(async () => {
  test.skip(!(await mockApiAvailable()), MOCK_API_SKIP_REASON);
});

/** Waits for both layout passes to finish: the canvas fades in once the graph is laid out and fitted. */
async function graphReady(page: Page) {
  await expect(page.locator('div.absolute.inset-0.bg-surface-muted')).toHaveClass(/opacity-100/);
}

/** A narrator card, found by the full name in its title attribute. */
const card = (page: Page, fullName: string) =>
  page.locator('.react-flow__node').filter({ has: page.locator(`[title="${fullName}"]`) });

const edge = (page: Page, id: string) => page.getByTestId(`rf__edge-${id}`);

test.describe('single tree (/tree/[id])', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/tree/x');
    await graphReady(page);
  });

  test('draws every narrator and link of the chain', async ({ page }) => {
    await expect(page.locator('.react-flow__node')).toHaveCount(5);
    await expect(page.locator('.react-flow__edge')).toHaveCount(4);
    await expect(card(page, 'محمد بن إسماعيل البخاري')).toContainText('المصدر والمُخَرِّج');
  });

  test('shows the grade of each narrator as text, not only as a colour', async ({ page }) => {
    await expect(card(page, 'راو متروك للاختبار')).toContainText('كذاب');
    await expect(card(page, 'عمر بن الخطاب')).toContainText('صحابي');
  });

  test('marks the broken link', async ({ page }) => {
    await expect(page.locator('.react-flow__edge-text', { hasText: 'انقطاع' })).toBeVisible();
    await expect(edge(page, 'e-N2-N1').locator('path.react-flow__edge-path')).toHaveCSS('stroke', 'rgb(239, 68, 68)');
  });

  test('a click opens the narrator details; Escape closes them', async ({ page }) => {
    await card(page, 'راو متروك للاختبار').click();

    const dialog = page.getByRole('dialog', { name: 'تفاصيل الراوي' });
    await expect(dialog).toBeVisible();
    await expect(dialog).toContainText('لا تشترك بلدانهما في الإقامة'); // the node's travel note reaches touch users
    await expect(dialog).toContainText('كذاب');

    await page.keyboard.press('Escape');
    await expect(dialog).toBeHidden();
  });

  test('«إبراز الضعفاء» dims the reliable narrators and brings them back', async ({ page }) => {
    const reliable = card(page, 'عبد الله بن الزبير الحميدي').locator('> div');
    await expect(reliable).toHaveCSS('opacity', '1');

    await page.getByRole('button', { name: 'إبراز الضعفاء' }).click();
    await expect(reliable).toHaveCSS('opacity', '0.3');
    await expect(card(page, 'راو متروك للاختبار').locator('> div')).toHaveCSS('opacity', '1');

    await page.getByRole('button', { name: 'إظهار الجميع' }).click();
    await expect(reliable).toHaveCSS('opacity', '1');
  });

  test('«فحص العلل» opens the report and marks the madar on the graph', async ({ page }) => {
    await page.getByRole('button', { name: /فحص العلل/ }).click();
    const panel = page.getByRole('dialog', { name: 'علل الحديث' });
    await expect(panel).toContainText('عنعنة مدلس');
    await expect(edge(page, 'e-N2-N1').locator('.react-flow__edge-text')).toContainText('عنعنة مدلس');
  });
});

test.describe('comparative tree (/takhreej)', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/takhreej?ids=h1,h2,h3');
    await graphReady(page);
  });

  test('merges three books into one graph', async ({ page }) => {
    // Compilers C, D, E and narrators N1, N2, N4, N5.
    await expect(page.locator('.react-flow__node')).toHaveCount(7);
    // N1→C, N1→D, N5→E, N2→N1, N2→N5, N4→N2.
    await expect(page.locator('.react-flow__edge')).toHaveCount(6);
    await expect(card(page, 'سفيان بن عيينة')).toContainText('مدلس');
  });

  test('a link used by one book takes that book\'s colour', async ({ page }) => {
    await expect(edge(page, 'e-N1-C').locator('path.react-flow__edge-path')).toHaveCSS('stroke', 'rgb(37, 99, 235)');
  });

  test('Ilal findings are laid over the edges and the madar is ringed', async ({ page }) => {
    await expect(edge(page, 'e-N2-N1').locator('.react-flow__edge-text')).toContainText('عنعنة مدلس');
    await expect(edge(page, 'e-N2-N1').locator('path.react-flow__edge-path')).toHaveCSS('stroke', 'rgb(234, 88, 12)');
    await expect(card(page, 'سفيان بن عيينة').locator('> div')).toHaveClass(/ring-amber-400/);
  });

  test('the legend focuses one book\'s routes; Escape clears it', async ({ page }) => {
    const legendButton = page.locator('button[title="إبراز مسارات هذا الكتاب"]', { hasText: 'صحيح مسلم' });
    await legendButton.click();
    await expect(legendButton).toHaveAttribute('aria-pressed', 'true');
    await expect(page.getByRole('button', { name: /إبراز: صحيح مسلم/ })).toBeVisible();

    await page.keyboard.press('Escape');
    await expect(page.getByRole('button', { name: /إبراز: صحيح مسلم/ })).toHaveCount(0);
    await expect(legendButton).toHaveAttribute('aria-pressed', 'false');
  });

  test('the sidebar shows the automatic grade, the matns and the Ilal tab', async ({ page }) => {
    const sidebar = page.locator('#takhreej-sidebar');
    await expect(sidebar).toContainText('التقدير الآلي للطرق');
    await expect(sidebar).toContainText('متون الروايات');

    await sidebar.getByRole('button', { name: /العلل/ }).click();
    await expect(sidebar).toContainText('عنعنة مدلس');
  });
});

test.describe('on a phone', () => {
  test.use({ viewport: { width: 375, height: 812 } });

  test('takhreej starts with the graph, and the matns open over it', async ({ page }) => {
    await page.goto('/takhreej?ids=h1,h2,h3');
    await graphReady(page);

    expect(await page.evaluate(() => document.documentElement.scrollWidth - innerWidth)).toBeLessThanOrEqual(0);
    await expect(page.locator('#takhreej-sidebar')).toHaveCSS('width', '0px');

    await page.getByRole('button', { name: 'إظهار المتون' }).click();
    await expect(page.locator('#takhreej-sidebar')).toContainText('متون الروايات');
    await page.getByRole('button', { name: /إغلاق والعودة للشجرة/ }).click();
    await expect(page.locator('#takhreej-sidebar')).toHaveCSS('width', '0px');
  });

  test('the tree page fits the screen and the legend starts collapsed', async ({ page }) => {
    await page.goto('/tree/x');
    await graphReady(page);

    expect(await page.evaluate(() => document.documentElement.scrollWidth - innerWidth)).toBeLessThanOrEqual(0);
    await expect(page.getByRole('button', { name: /مفتاح الرموز/ })).toBeVisible();
    await card(page, 'راو متروك للاختبار').click(); // not covered by an open legend
    await expect(page.getByRole('dialog', { name: 'تفاصيل الراوي' })).toBeVisible();
  });
});
