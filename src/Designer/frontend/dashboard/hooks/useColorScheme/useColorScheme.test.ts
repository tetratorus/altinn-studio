import { act, renderHook } from '@testing-library/react';
import { typedLocalStorage } from '@studio/pure-functions';
import { useColorScheme } from './useColorScheme';
import { ColorScheme } from '../../enums/ColorScheme';
import {
  COLOR_SCHEME_STORAGE_KEY,
  DEFAULT_COLOR_SCHEME,
  storeColorScheme,
} from '../../utils/colorSchemeUtils';

describe('useColorScheme', () => {
  afterEach(() => {
    typedLocalStorage.removeItem(COLOR_SCHEME_STORAGE_KEY);
    document.documentElement.removeAttribute('data-color-scheme');
  });

  it('returns the default color scheme and applies it when nothing is stored', () => {
    const { result } = renderHook(() => useColorScheme());
    expect(result.current[0]).toBe(DEFAULT_COLOR_SCHEME);
    expect(document.documentElement).toHaveAttribute('data-color-scheme', DEFAULT_COLOR_SCHEME);
  });

  it('returns the stored color scheme', () => {
    storeColorScheme(ColorScheme.Auto);
    const { result } = renderHook(() => useColorScheme());
    expect(result.current[0]).toBe(ColorScheme.Auto);
  });

  it('stores and applies the color scheme when it is set', () => {
    const { result } = renderHook(() => useColorScheme());

    act(() => result.current[1](ColorScheme.Dark));

    expect(result.current[0]).toBe(ColorScheme.Dark);
    expect(typedLocalStorage.getItem(COLOR_SCHEME_STORAGE_KEY)).toBe(ColorScheme.Dark);
    expect(document.documentElement).toHaveAttribute('data-color-scheme', ColorScheme.Dark);
  });
});
