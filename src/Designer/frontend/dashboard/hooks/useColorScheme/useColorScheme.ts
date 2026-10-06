import { useCallback, useEffect, useState } from 'react';
import type { ColorScheme } from '../../enums/ColorScheme';
import {
  applyColorScheme,
  getStoredColorScheme,
  storeColorScheme,
} from '../../utils/colorSchemeUtils';

export function useColorScheme(): [ColorScheme, (colorScheme: ColorScheme) => void] {
  const [colorScheme, setColorSchemeState] = useState<ColorScheme>(getStoredColorScheme);

  useEffect(() => {
    applyColorScheme(colorScheme);
  }, [colorScheme]);

  const setColorScheme = useCallback((newColorScheme: ColorScheme): void => {
    storeColorScheme(newColorScheme);
    setColorSchemeState(newColorScheme);
  }, []);

  return [colorScheme, setColorScheme];
}
