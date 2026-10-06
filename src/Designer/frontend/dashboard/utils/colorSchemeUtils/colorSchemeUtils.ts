import { typedLocalStorage } from '@studio/pure-functions';
import { ColorScheme } from '../../enums/ColorScheme';

export const COLOR_SCHEME_STORAGE_KEY = 'dashboard-color-scheme';
export const DEFAULT_COLOR_SCHEME: ColorScheme = ColorScheme.Light;

export function isColorScheme(value: unknown): value is ColorScheme {
  return Object.values(ColorScheme).includes(value as ColorScheme);
}

export function getStoredColorScheme(): ColorScheme {
  const storedValue: unknown = typedLocalStorage.getItem<ColorScheme>(COLOR_SCHEME_STORAGE_KEY);
  return isColorScheme(storedValue) ? storedValue : DEFAULT_COLOR_SCHEME;
}

export function storeColorScheme(colorScheme: ColorScheme): void {
  typedLocalStorage.setItem<ColorScheme>(COLOR_SCHEME_STORAGE_KEY, colorScheme);
}

export function applyColorScheme(
  colorScheme: ColorScheme,
  element: HTMLElement = document.documentElement,
): void {
  element.setAttribute('data-color-scheme', colorScheme);
}
