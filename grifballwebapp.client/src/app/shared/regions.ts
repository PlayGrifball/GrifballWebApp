/** A region a player plays from: Other.Regions, seeded with these fixed IDs (RegionConfiguration on the server). */
export interface Region {
  regionID: number;
  regionName: string;
}

export const REGIONS: readonly Region[] = [
  { regionID: 1, regionName: 'West North America' },
  { regionID: 2, regionName: 'Central North America' },
  { regionID: 3, regionName: 'East North America' },
  { regionID: 4, regionName: 'North Europe' },
  { regionID: 5, regionName: 'South Europe' },
  { regionID: 6, regionName: 'Australia' },
  { regionID: 7, regionName: 'Europe' },
];
