import type { UseQueryResult } from '@tanstack/react-query';
import { useQuery } from '@tanstack/react-query';
import type { IRuleModelFieldElement } from '../../types/global';
import { useServicesContext } from 'app-shared/contexts/ServicesContext';
import { QueryKey } from 'app-shared/types/QueryKey';
import { extractRuleModelFields } from '../../utils/ruleHandlerSandbox';

export const useRuleModelQuery = (
  org: string,
  app: string,
  layoutSetName: string,
): UseQueryResult<IRuleModelFieldElement[]> => {
  const { getRuleModel } = useServicesContext();
  return useQuery<IRuleModelFieldElement[]>({
    queryKey: [QueryKey.RuleHandler, org, app, layoutSetName],
    queryFn: () =>
      getRuleModel(org, app, layoutSetName).then((ruleModel) =>
        ruleModel ? extractRuleModelFields(ruleModel) : [],
      ),
  });
};
