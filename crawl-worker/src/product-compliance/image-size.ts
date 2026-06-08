import { getBrowser } from "../playwright/render.js";

const enabled = process.env.PLAYWRIGHT_ENABLED !== "false";
const maxChecks = Number(process.env.PRODUCT_COMPLIANCE_IMAGE_CHECKS ?? "20");
let checkCount = 0;

export function resetImageCheckBudget(): void {
  checkCount = 0;
}

export async function measureImageUrl(
  imageUrl: string,
): Promise<{ width: number; height: number } | null> {
  if (!enabled || checkCount >= maxChecks) return null;
  if (!imageUrl.startsWith("http://") && !imageUrl.startsWith("https://")) return null;

  checkCount++;
  const browser = await getBrowser();
  const context = await browser.newContext();
  const page = await context.newPage();
  try {
    const dims = await page.evaluate(async (url) => {
      return new Promise<{ width: number; height: number } | null>((resolve) => {
        const img = new Image();
        img.onload = () => resolve({ width: img.naturalWidth, height: img.naturalHeight });
        img.onerror = () => resolve(null);
        img.src = url;
      });
    }, imageUrl);
    return dims;
  } catch {
    return null;
  } finally {
    await context.close();
  }
}
