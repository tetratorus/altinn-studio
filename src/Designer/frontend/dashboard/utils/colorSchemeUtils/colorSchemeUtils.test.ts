import { typedLocalStorage } from '@studio/pure-functions';
import { ColorScheme } from '../../enums/ColorScheme';
import {
  COLOR_SCHEME_STORAGE_KEY,
  DEFAULT_COLOR_SCHEME,
  applyColorScheme,
  getStoredColorScheme,
  isColorScheme,
  storeColorScheme,
} from './colorSchemeUtils';

describe('colorSchemeUtils', () => {
  afterEach(() => {
    typedLocalStorage.removeItem(COLOR_SCHEME_STORAGE_KEY);
    document.documentElement.removeAttribute('data-color-scheme');
  });

  describe('isColorScheme', () => {
    it.each(Object.values(ColorScheme))('returns true for %s', (colorScheme) => {
      expect(isColorScheme(colorScheme)).toBe(true);
    });

    it.each(['blue', '', null, undefined, 1])('returns false for %p', (value) => {
      expect(isColorScheme(value)).toBe(false);
    });
  });

  describe('getStoredColorScheme', () => {
    it('returns the default color scheme when nothing is stored', () => {
      expect(getStoredColorScheme()).toBe(DEFAULT_COLOR_SCHEME);
    });

    it('returns the stored color scheme', () => {
      storeColorScheme(ColorScheme.Dark);
      expect(getStoredColorScheme()).toBe(ColorScheme.Dark);
    });

    it('returns the default color scheme when the stored value is invalid', () => {
      typedLocalStorage.setItem(COLOR_SCHEME_STORAGE_KEY, 'invalid');
      expect(getStoredColorScheme()).toBe(DEFAULT_COLOR_SCHEME);
    });
  });

  describe('applyColorScheme', () => {
    it('sets the data-color-scheme attribute on the document element by default', () => {
      applyColorScheme(ColorScheme.Dark);
      expect(document.documentElement).toHaveAttribute('data-color-scheme', ColorScheme.Dark);
    });

    it('sets the data-color-scheme attribute on the given element', () => {
      const element = document.createElement('div');
      applyColorScheme(ColorScheme.Auto, element);
      expect(element).toHaveAttribute('data-color-scheme', ColorScheme.Auto);
    });
  });
});
