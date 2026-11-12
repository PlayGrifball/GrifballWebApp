import type { DeploymentFinish } from '@api/models';
import { deploymentFinishLabel } from './deploymentFinish';

describe('deploymentFinishLabel', () => {
  it('names a champion by the tiers it won, and other finishes by their label', () => {
    expect(deploymentFinishLabel({ finish: 'Champion', championshipTiers: ['Amateur', 'Pro'] })).toBe('Amateur & Pro Champion');
    expect(deploymentFinishLabel({ finish: 'Champion', championshipTiers: [] })).toBe('Champion');
    expect(deploymentFinishLabel({ finish: 'RegularSeason', championshipTiers: [] })).toBe('Regular season');
  });

  it('shows a finish it does not know by its name', () => {
    expect(deploymentFinishLabel({ finish: 'Relegated' as DeploymentFinish, championshipTiers: [] })).toBe('Relegated');
  });
});
