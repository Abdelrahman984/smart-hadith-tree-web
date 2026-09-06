import { test, expect } from '@playwright/test';

test('homepage has correct title and elements', async ({ page }) => {
  await page.goto('/');

  // Check the title or main heading
  const heading = page.locator('h1');
  await expect(heading).toContainText('شجرة الأسانيد الذكية');

  // Check if the call to action button is visible
  const startButton = page.locator('text=ابدأ البحث في الأحاديث');
  await expect(startButton).toBeVisible();
});

test('can navigate to search page from homepage', async ({ page }) => {
  await page.goto('/');
  
  const startButton = page.locator('text=ابدأ البحث في الأحاديث');
  await startButton.click();

  // URL should contain /search
  await expect(page).toHaveURL(/.*\/search/);
});
