import { renderHookWithMockStore } from '../../testing/mocks';
import { waitFor } from '@testing-library/react';
import { useRuleModelQuery } from './useRuleModelQuery';
import ruleHandlerMock from '../../testing/ruleHandlerMock';
import { createQueryClientMock } from 'app-shared/mocks/queryClientMock';
import type { ServicesContextProps } from 'app-shared/contexts/ServicesContext';
import { app, org } from '@studio/testing/testids';
import { layoutSet1NameMock } from '@altinn/ux-editor-v3/testing/layoutSetsMock';
import { extractRuleModelFields } from '../../utils/ruleHandlerSandbox';

jest.mock('../../utils/ruleHandlerSandbox', () => ({
  extractRuleModelFields: jest.fn(),
}));

// Test data:
const selectedLayoutSet = layoutSet1NameMock;
const extractedFields = [
  { name: 'rule', inputs: { field: 'label' }, type: 'rule' as const },
  { name: 'condition', inputs: { field: 'label' }, type: 'condition' as const },
];

const getRuleModel = jest.fn().mockImplementation(() => Promise.resolve(ruleHandlerMock));
const mockedExtractRuleModelFields = jest.mocked(extractRuleModelFields);

describe('useRuleModelQuery', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockedExtractRuleModelFields.mockResolvedValue(extractedFields);
  });

  it('Calls getRuleModel with correct parameters', async () => {
    await renderAndWaitForSuccess({ getRuleModel });
    expect(getRuleModel).toHaveBeenCalledTimes(1);
    expect(getRuleModel).toHaveBeenCalledWith(org, app, selectedLayoutSet);
  });

  it('returns the fields extracted by the sandbox and passes the raw rule handler to it', async () => {
    const { result } = await renderAndWaitForSuccess({ getRuleModel });
    expect(result.current.data).toEqual(extractedFields);
    expect(mockedExtractRuleModelFields).toHaveBeenCalledWith(ruleHandlerMock);
  });

  it('returns [] and does not invoke the sandbox when the rule handler is empty or null', async () => {
    const emptyResult = await renderAndWaitForSuccess({
      getRuleModel: () => Promise.resolve(''),
    });
    expect(emptyResult.result.current.data).toEqual([]);
    expect(mockedExtractRuleModelFields).not.toHaveBeenCalled();

    const nullResult = await renderAndWaitForSuccess({
      getRuleModel: () => Promise.resolve(null),
    });
    expect(nullResult.result.current.data).toEqual([]);
    expect(mockedExtractRuleModelFields).not.toHaveBeenCalled();
  });
});

const renderAndWaitForSuccess = async (
  queries: Partial<ServicesContextProps> = {},
  queryClient = createQueryClientMock(),
) => {
  const { renderHookResult } = renderHookWithMockStore(
    {},
    queries,
    queryClient,
  )(() => useRuleModelQuery(org, app, selectedLayoutSet));
  await waitFor(() => expect(renderHookResult.result.current.isSuccess).toBe(true));
  return renderHookResult;
};
