import fs from 'node:fs/promises';
import path from 'node:path';

const OUT_DIR = path.resolve('data/historical-archive');
const RAW_DIR = path.join(OUT_DIR, 'raw');

export const SOURCES = [
  // === AGLA 2018 ===
  { id: '2018-sl18-pro-schedule', category: 'AGLA-2018', year: 2018, title: 'SL18 Pro Schedule DEAD', url: 'https://docs.google.com/spreadsheets/d/1JR5LFsvurVDJDan0J41sUvpdEmU_bCrBTQdyvT_QPkc/edit#gid=763392737', sheetId: '1JR5LFsvurVDJDan0J41sUvpdEmU_bCrBTQdyvT_QPkc', gid: '763392737', type: 'gsheet' },
  { id: '2018-sl18-legacy-schedule', category: 'AGLA-2018', year: 2018, title: 'SL18 Legacy Schedule DEAD', url: 'https://docs.google.com/spreadsheets/d/1RRoDr9bmj5g2RByTBFQDzH3H-fsFm1u_RSEw_NV2MDE/edit#gid=763392737', sheetId: '1RRoDr9bmj5g2RByTBFQDzH3H-fsFm1u_RSEw_NV2MDE', gid: '763392737', type: 'gsheet' },
  { id: '2018-sl18-ammy-schedule', category: 'AGLA-2018', year: 2018, title: 'SL18 Ammy Schedule DEAD', url: 'https://docs.google.com/spreadsheets/d/1srKzhMz0keQSWpKGQR5P3281KrGCBzL0tLp_FHRez9k/edit#gid=763392737', sheetId: '1srKzhMz0keQSWpKGQR5P3281KrGCBzL0tLp_FHRez9k', gid: '763392737', type: 'gsheet' },
  { id: '2018-sml18-stats', category: 'AGLA-2018', year: 2018, title: 'SML18 Stats', url: 'http://grifballstats.com/AGLASML18Stats.php', type: 'web' },
  { id: '2018-sml18-fantasy-rey', category: 'AGLA-2018', year: 2018, title: 'SML18 Fantasy by Rey', url: 'https://docs.google.com/spreadsheets/d/1iHaq1roCJrTSw2_d8DWnqln4jcyBmBdu5-0lucv7bF4', sheetId: '1iHaq1roCJrTSw2_d8DWnqln4jcyBmBdu5-0lucv7bF4', type: 'gsheet' },
  { id: '2018-fl18-bid-league', category: 'AGLA-2018', year: 2018, title: 'FL18 Bid League', url: 'https://docs.google.com/spreadsheets/d/1Th2BrmlN6PdYfgoMXv4N5tZQMsfDsQAUZxkB0l3wOUs/edit?usp=sharing', sheetId: '1Th2BrmlN6PdYfgoMXv4N5tZQMsfDsQAUZxkB0l3wOUs', type: 'gsheet' },
  { id: '2018-fl18-schedule', category: 'AGLA-2018', year: 2018, title: 'FL18 Schedule DEAD', url: 'https://docs.google.com/spreadsheets/d/1KKf9NQ39c9-0uA4VFGVBCs8trti4JE1xZNe0z-OBV-8/edit?usp=sharing', sheetId: '1KKf9NQ39c9-0uA4VFGVBCs8trti4JE1xZNe0z-OBV-8', type: 'gsheet' },

  // === AGLA 2019 ===
  { id: '2019-wl19-schedule-stats', category: 'AGLA-2019', year: 2019, title: 'WL19 Schedule & Stats', url: 'https://docs.google.com/spreadsheets/d/1eFkwBB9dPex_Cub0ZKFYmTrqXvBKZcp1LHMafCQYjds/edit?usp=sharing', sheetId: '1eFkwBB9dPex_Cub0ZKFYmTrqXvBKZcp1LHMafCQYjds', type: 'gsheet' },
  { id: '2019-wl19-playoff-bracket', category: 'AGLA-2019', year: 2019, title: 'WL19 Playoff Bracket', url: 'https://challonge.com/h3wl19', type: 'challonge', challongeSlug: 'h3wl19' },
  { id: '2019-sl19-schedule-stats', category: 'AGLA-2019', year: 2019, title: 'SL19 Schedule & Stats', url: 'https://docs.google.com/spreadsheets/d/1hWlxvPYIwlUKDIcnyrVA5FYWiYgLegGy2fbraz9XkPY/edit?usp=sharing', sheetId: '1hWlxvPYIwlUKDIcnyrVA5FYWiYgLegGy2fbraz9XkPY', type: 'gsheet' },

  // === AGLA 2020 ===
  { id: '2020-wl20-xbox-schedule', category: 'AGLA-2020', year: 2020, title: 'WL20 Xbox Schedule DEAD', url: 'https://docs.google.com/spreadsheets/d/1U83qbjsQR0Up5mPzxs5IyzB4YclVXVDX7oOgw2oeUio/edit?usp=drivesdk', sheetId: '1U83qbjsQR0Up5mPzxs5IyzB4YclVXVDX7oOgw2oeUio', type: 'gsheet' },
  { id: '2020-sl20-console-h5', category: 'AGLA-2020', year: 2020, title: 'SL20 Console Halo 5', url: 'https://docs.google.com/spreadsheets/d/1snAr7pMsghrUt5X11vuSEaHIN_2tPNudl2nJUfzuS0M/edit?ts=5e8a3140#gid=1623708663', sheetId: '1snAr7pMsghrUt5X11vuSEaHIN_2tPNudl2nJUfzuS0M', gid: '1623708663', type: 'gsheet' },
  { id: '2020-sl20-console-reach', category: 'AGLA-2020', year: 2020, title: 'SL20 Console Reach', url: 'https://docs.google.com/spreadsheets/d/1ScAIlAnYjdgMfp-Y9MGiYpb-IaiOOddvwtM4yv02tRU/edit#gid=1715716983', sheetId: '1ScAIlAnYjdgMfp-Y9MGiYpb-IaiOOddvwtM4yv02tRU', gid: '1715716983', type: 'gsheet' },
  { id: '2020-sml20-halo3', category: 'AGLA-2020', year: 2020, title: 'SML20 Halo 3', url: 'https://docs.google.com/spreadsheets/d/15FUtOUopmoLyQ488O8Z_Z-dEFGfWrOMhmOarHgscUc8/edit#gid=0', sheetId: '15FUtOUopmoLyQ488O8Z_Z-dEFGfWrOMhmOarHgscUc8', gid: '0', type: 'gsheet' },
  { id: '2020-fl20-pc-schedule-stats', category: 'AGLA-2020', year: 2020, title: 'FL20 PC schedule, stats, standings', url: 'https://docs.google.com/spreadsheets/d/1UFQZ6ZXEkmzDpdsUN0hlyTouzB9RtRwJd63V_NgpLnw/edit?usp=sharing', sheetId: '1UFQZ6ZXEkmzDpdsUN0hlyTouzB9RtRwJd63V_NgpLnw', type: 'gsheet' },

  // === AGLA 2021 ===
  { id: '2021-wl21-schedule', category: 'AGLA-2021', year: 2021, title: 'WL21 Schedule', url: 'https://docs.google.com/spreadsheets/d/1XepEDC1U3pi0y0bArKJFog6dA_EVC3YX5O5bVXkoLKQ/edit?ts=60097c1a#gid=1799145928', sheetId: '1XepEDC1U3pi0y0bArKJFog6dA_EVC3YX5O5bVXkoLKQ', gid: '1799145928', type: 'gsheet' },
  { id: '2021-wl21-legacy-stats', category: 'AGLA-2021', year: 2021, title: 'WL21 Legacy Stats', url: 'https://docs.google.com/spreadsheets/d/1GmCC5gG6kG2oWfaweC5hKRuUWFc1EfcmKxP8y0iN5Ls/edit#gid=1752549387', sheetId: '1GmCC5gG6kG2oWfaweC5hKRuUWFc1EfcmKxP8y0iN5Ls', gid: '1752549387', type: 'gsheet' },
  { id: '2021-wl21-rookie-stats', category: 'AGLA-2021', year: 2021, title: 'WL21 Rookie stats', url: 'https://docs.google.com/spreadsheets/d/1nJ6ljk9vCgHL6O_PqJyQ8l0nYNFkJ9mxB6S1GnmqXp0/edit?usp=sharing', sheetId: '1nJ6ljk9vCgHL6O_PqJyQ8l0nYNFkJ9mxB6S1GnmqXp0', type: 'gsheet' },
  { id: '2021-sml21-pro-stats', category: 'AGLA-2021', year: 2021, title: 'SML21 Pro Stats', url: 'https://docs.google.com/spreadsheets/d/1a4rzrIgmGYJMhAh7OWKqflRBlcojPKbjESd6z-vdYSQ/edit?usp=sharing', sheetId: '1a4rzrIgmGYJMhAh7OWKqflRBlcojPKbjESd6z-vdYSQ', type: 'gsheet' },
  { id: '2021-sml21-ammy-stats', category: 'AGLA-2021', year: 2021, title: 'SML21 Ammy Stats', url: 'https://docs.google.com/spreadsheets/d/1uyoSop8S-rclI8bKMrxfoHz_gAor6PEr4KYWrzNVeUk/edit?usp=sharing', sheetId: '1uyoSop8S-rclI8bKMrxfoHz_gAor6PEr4KYWrzNVeUk', type: 'gsheet' },

  // === AGLA 2022 ===
  { id: '2022-h4-bid-sheet', category: 'AGLA-2022', year: 2022, title: "ASpence's Halo 4 Bid Season Bid Sheet", url: 'https://docs.google.com/spreadsheets/d/13mh57_f3lTZ1_J73w79yyzCnDJwUhKI3/edit?usp=sharing&ouid=115452215758315537570&rtpof=true&sd=true', sheetId: '13mh57_f3lTZ1_J73w79yyzCnDJwUhKI3', type: 'gsheet' },
  { id: '2022-h4-bid-draft', category: 'AGLA-2022', year: 2022, title: 'ASpence Halo 4 Bid Season Draft DEAD', url: 'https://docs.google.com/spreadsheets/d/1qAKUcr1MdYW9UVmXRLd0C-MfG8iLtYtIGHuSqXDnGCA/edit?usp=sharing', sheetId: '1qAKUcr1MdYW9UVmXRLd0C-MfG8iLtYtIGHuSqXDnGCA', type: 'gsheet' },
  { id: '2022-h4-bid-stats', category: 'AGLA-2022', year: 2022, title: 'ASpence Halo 4 Bid Season Stats', url: 'https://docs.google.com/spreadsheets/d/1Z9VmOuCMTgaEnOnUU9umVga-2SkJO4_v/edit?usp=sharing&ouid=115452215758315537570&rtpof=true&sd=true', sheetId: '1Z9VmOuCMTgaEnOnUU9umVga-2SkJO4_v', type: 'gsheet' },
  { id: '2022-h3-action-schedule', category: 'AGLA-2022', year: 2022, title: 'H3 Action League 22 Schedule', url: 'https://docs.google.com/spreadsheets/d/1ceO9r3ZgOOc7IZ5emFypcfYQh9HmWC4UmX5idkBKfv0/edit#gid=1256495517', sheetId: '1ceO9r3ZgOOc7IZ5emFypcfYQh9HmWC4UmX5idkBKfv0', gid: '1256495517', type: 'gsheet' },
  { id: '2022-h3-action-stats', category: 'AGLA-2022', year: 2022, title: 'H3 Action League 22 Stats', url: 'https://docs.google.com/spreadsheets/d/1dti38ND1NcUVgofoJSFban2oeC05lYky/edit?usp=sharing&ouid=115452215758315537570&rtpof=true&sd=true', sheetId: '1dti38ND1NcUVgofoJSFban2oeC05lYky', type: 'gsheet' },

  // === AGLA 2023 (Infinite Season 1) ===
  { id: '2023-da-wl23-stats', category: 'AGLA-2023', year: 2023, title: 'Grifball DA WL23 Stats', url: 'https://docs.google.com/spreadsheets/d/1xpraseVR_rQu7LCuUNWuFWf4qSHcLyJlxDN2RtMF240/edit?usp=sharing', sheetId: '1xpraseVR_rQu7LCuUNWuFWf4qSHcLyJlxDN2RtMF240', type: 'gsheet' },
  { id: '2023-da-wl23-schedule', category: 'AGLA-2023', year: 2023, title: 'Grifball DA WL23 Schedule', url: 'https://docs.google.com/spreadsheets/d/10wxiYzEgZb4H6cZhZMw0v-k0yrDvaOpi/edit?usp=sharing&ouid=115452215758315537570&rtpof=true&sd=true', sheetId: '10wxiYzEgZb4H6cZhZMw0v-k0yrDvaOpi', type: 'gsheet' },

  // === AGLA 2024 (Infinite Season 2) ===
  { id: '2024-da-sl24-stats', category: 'AGLA-2024', year: 2024, title: 'Grifball DA(Post Netcode Change) SL24 Stats', url: 'https://docs.google.com/spreadsheets/d/1xJE_9vJyvXWAet8VyYdXpZeZDPwEMlQL/edit?usp=sharing&ouid=115452215758315537570&rtpof=true&sd=true', sheetId: '1xJE_9vJyvXWAet8VyYdXpZeZDPwEMlQL', type: 'gsheet' },
  { id: '2024-da-sl24-schedule', category: 'AGLA-2024', year: 2024, title: 'Grifball DA(Post Netcode Change) SL24 Schedule', url: 'https://docs.google.com/spreadsheets/d/13QrNos6NEb8aEFWtYrTn5-8-ayTiQdxs/edit?usp=sharing&ouid=115452215758315537570&rtpof=true&sd=true', sheetId: '13QrNos6NEb8aEFWtYrTn5-8-ayTiQdxs', type: 'gsheet' },

  // === AGLA 2025 (Infinite Season 4-8) ===
  { id: '2025-octane-wl25-stats', category: 'AGLA-2025', year: 2025, title: 'Octane WL25 Stats', url: 'https://docs.google.com/spreadsheets/d/1L2F7zzUsZ90c7iFxZVn9xkXdj17w7e72/edit?usp=sharing&ouid=115452215758315537570&rtpof=true&sd=true', sheetId: '1L2F7zzUsZ90c7iFxZVn9xkXdj17w7e72', type: 'gsheet' },
  { id: '2025-octane-wl25-schedule', category: 'AGLA-2025', year: 2025, title: 'Octane WL25 Schedule', url: 'https://docs.google.com/spreadsheets/d/1-zTTfLK4RgxlTJfzWEGhI-vaecS6UOSLhZq3ikTyzA4/edit?usp=sharing', sheetId: '1-zTTfLK4RgxlTJfzWEGhI-vaecS6UOSLhZq3ikTyzA4', type: 'gsheet' },
  { id: '2025-octane-wl25-bid-chart', category: 'AGLA-2025', year: 2025, title: 'Octane WL25 Bid Chart', url: 'https://docs.google.com/spreadsheets/d/1hJcR7bpQIZ7ebopNebq3A6iL0Lde1KQGdX8fnA8LndY/edit?gid=262963722#gid=262963722', sheetId: '1hJcR7bpQIZ7ebopNebq3A6iL0Lde1KQGdX8fnA8LndY', gid: '262963722', type: 'gsheet' },
  { id: '2025-octane-sl25-bid-chart', category: 'AGLA-2025', year: 2025, title: 'Octane SL25 Bid Chart', url: 'https://docs.google.com/spreadsheets/d/14MbNZubCiUn-IpZscrYvuTL-FA4P2HbJXzMrW7p710M/edit?gid=333209559#gid=333209559', sheetId: '14MbNZubCiUn-IpZscrYvuTL-FA4P2HbJXzMrW7p710M', gid: '333209559', type: 'gsheet' },
  { id: '2025-octane-sl25-bid-schedule', category: 'AGLA-2025', year: 2025, title: 'Octane SL25 Bid Schedule', url: 'https://docs.google.com/spreadsheets/d/105G1WCYNVSm922_1I_A3-v2QQkBZ4u0q/edit?usp=sharing&ouid=115452215758315537570&rtpof=true&sd=true', sheetId: '105G1WCYNVSm922_1I_A3-v2QQkBZ4u0q', type: 'gsheet' },
  { id: '2025-octane-sl25-bid-stats', category: 'AGLA-2025', year: 2025, title: 'Octane SL25 Bid Stats', url: 'https://docs.google.com/spreadsheets/d/1tuCF7JpLWUuG1utZlMG6588FQmcRJOt6yRemr5cocZg/edit?usp=drivesdk', sheetId: '1tuCF7JpLWUuG1utZlMG6588FQmcRJOt6yRemr5cocZg', type: 'gsheet' },
  { id: '2025-classic-sml25-draft', category: 'AGLA-2025', year: 2025, title: 'Classic SML25 Pro & Legacy Draft Sheets', url: 'https://docs.google.com/spreadsheets/d/1omgOFqcSiWVgZMffVWhHzejeSSSm9xO6da217L2TIng/edit?usp=sharing', sheetId: '1omgOFqcSiWVgZMffVWhHzejeSSSm9xO6da217L2TIng', type: 'gsheet' },
  { id: '2025-classic-sml25-schedules', category: 'AGLA-2025', year: 2025, title: 'Classic SML25 Pro & Legacy Schedules', url: 'https://docs.google.com/spreadsheets/d/1qC2fJtagt_KPQI73fsZpmms0cXKMpln6a4M5a_8d4aU/edit?gid=446236773#gid=446236773', sheetId: '1qC2fJtagt_KPQI73fsZpmms0cXKMpln6a4M5a_8d4aU', gid: '446236773', type: 'gsheet' },
  { id: '2025-classic-sml25-pro-stats', category: 'AGLA-2025', year: 2025, title: 'Classic SML25 Pro League Stats', url: 'https://docs.google.com/spreadsheets/d/1iihXHoOw2vF9klgNPOJSCVQT2rmptwhKX9xLCuCnvNI/edit?usp=sharing', sheetId: '1iihXHoOw2vF9klgNPOJSCVQT2rmptwhKX9xLCuCnvNI', type: 'gsheet' },
  { id: '2025-classic-sml25-legacy-stats', category: 'AGLA-2025', year: 2025, title: 'Classic SML25 Legacy League Stats', url: 'https://docs.google.com/spreadsheets/d/1C8lcTq9HWAFEZWQJYSD189nf7pic_QiLtOfqSGlzxv8/edit?gid=1591366199#gid=1591366199', sheetId: '1C8lcTq9HWAFEZWQJYSD189nf7pic_QiLtOfqSGlzxv8', gid: '1591366199', type: 'gsheet' },
  { id: '2026-sl26-minor-stats-schedule', category: 'AGLA-2025', year: 2026, title: 'SL26 Minor Season Stats & Schedule', url: 'https://docs.google.com/spreadsheets/d/1oXOghXN493YwkEobhgL3UcDoiG8f0sameZ3QagrYUoI/edit?gid=1536689010#gid=1536689010', sheetId: '1oXOghXN493YwkEobhgL3UcDoiG8f0sameZ3QagrYUoI', gid: '1536689010', type: 'gsheet' },
  { id: '2026-sl26-major-stats', category: 'AGLA-2025', year: 2026, title: 'SL26 Major Season Stats', url: 'https://docs.google.com/spreadsheets/d/1aN6Q8fq4SEi-TTHsHCB2BNggcWSfjzcGyhiL7Sf2dIQ/edit?gid=1269633556#gid=1269633556', sheetId: '1aN6Q8fq4SEi-TTHsHCB2BNggcWSfjzcGyhiL7Sf2dIQ', gid: '1269633556', type: 'gsheet' },
  { id: '2026-sl26-major-schedule', category: 'AGLA-2025', year: 2026, title: 'SL26 Major Season Schedule', url: 'https://docs.google.com/spreadsheets/d/1DBxPmiTg6yGyL7fjy5NmwSL0sr2ei1thnXD-vP32oNk/edit?gid=1882778066#gid=1882778066', sheetId: '1DBxPmiTg6yGyL7fjy5NmwSL0sr2ei1thnXD-vP32oNk', gid: '1882778066', type: 'gsheet' },

  // === IGL Seasons (Infinite Season 3) ===
  { id: '2024-igl-evolved-fl24', category: 'IGL-2024', year: 2024, title: 'Evolved FL24', url: 'https://docs.google.com/spreadsheets/d/1schmXjzORQ75nk63hR3QZGtUZsONwDsQNiis-5gm33I/edit?pli=1&gid=2107150591#gid=2107150591', sheetId: '1schmXjzORQ75nk63hR3QZGtUZsONwDsQNiis-5gm33I', gid: '2107150591', type: 'gsheet' },
  { id: '2024-igl-evolved-fl24-stats', category: 'IGL-2024', year: 2024, title: 'Evolved FL24 Stats', url: 'https://docs.google.com/spreadsheets/d/1x3UX4S0ehZdbGlegX8RZ5lxn0ydooIRGkdvXYc_tPMQ/edit?usp=drivesdk', sheetId: '1x3UX4S0ehZdbGlegX8RZ5lxn0ydooIRGkdvXYc_tPMQ', type: 'gsheet' },
  { id: '2024-igl-infektions-fl24-stats', category: 'IGL-2024', year: 2024, title: 'Infektions Halo Infinite FL24 Stats', url: 'https://1drv.ms/x/c/da0832788fab0c31/Eez6MhXmXjBLpbPcnY6-uUsBD6QtowNmAKlIg3uiBTBLmA?e=bhY4op', type: 'onedrive' },

  // === GWL Seasons ===
  { id: 'gwl-ts1-halo4', category: 'GWL', year: 2022, title: 'Tournament Series #1 Halo 4', url: 'https://docs.google.com/spreadsheets/d/1x0KQ4bR2jXkjdzjQWx6BJtFsF8XVXAKg/edit?usp=sharing&ouid=115452215758315537570&rtpof=true&sd=true', sheetId: '1x0KQ4bR2jXkjdzjQWx6BJtFsF8XVXAKg', type: 'gsheet' },
  { id: 'gwl-ts2-reach', category: 'GWL', year: 2022, title: 'Tournament Series #2 Halo Reach', url: 'https://docs.google.com/spreadsheets/d/1Dh1cCnsy0Zn5B8iamQ-YajU1wQi94ti_/edit?usp=sharing&ouid=115452215758315537570&rtpof=true&sd=true', sheetId: '1Dh1cCnsy0Zn5B8iamQ-YajU1wQi94ti_', type: 'gsheet' },
  { id: '2023-gwl-sl23-reach-stats', category: 'GWL', year: 2023, title: 'SL23 Halo Reach Stats', url: 'https://docs.google.com/spreadsheets/d/11cheA4Rt49b97I9fLkROyPxHyZaGz-2e/edit?usp=sharing&ouid=115452215758315537570&rtpof=true&sd=true', sheetId: '11cheA4Rt49b97I9fLkROyPxHyZaGz-2e', type: 'gsheet' },
  { id: '2023-gwl-sl23-reach-schedule', category: 'GWL', year: 2023, title: 'SL23 Halo Reach Schedule/Playoffs', url: 'https://docs.google.com/spreadsheets/d/1PWxSocTL6BPYU6XB6QSaS075IG4CD6N5/edit?usp=sharing&ouid=115452215758315537570&rtpof=true&sd=true', sheetId: '1PWxSocTL6BPYU6XB6QSaS075IG4CD6N5', type: 'gsheet' },

  // === Side Tournaments 2021 ===
  { id: '2021-side-djblue-reach-stats', category: 'Side-2021', year: 2021, title: "DJBluePDX's Grifball Summer Series Presented By Deamhack - Halo Reach - Stats", url: 'https://docs.google.com/spreadsheets/d/1J1g1l23icDecoB9yUeDgbmbx5-PAMK75MVv3my70AMU/edit?usp=sharing', sheetId: '1J1g1l23icDecoB9yUeDgbmbx5-PAMK75MVv3my70AMU', type: 'gsheet' },
  { id: '2021-side-djblue-h3-stats', category: 'Side-2021', year: 2021, title: "DJBluePDX's Grifball Summer Series Presented By Deamhack - Halo 3 -Stats", url: 'https://docs.google.com/spreadsheets/d/1_d4c8D42qycl03bs_qb_Lr2a-dvRpKzwkOkGl389_cw/edit?usp=sharing', sheetId: '1_d4c8D42qycl03bs_qb_Lr2a-dvRpKzwkOkGl389_cw', type: 'gsheet' },
  { id: '2021-side-respawn-h4-1', category: 'Side-2021', year: 2021, title: 'The Respawn All-Stars Halo 4 Grifball Fall Series (1/3) 10/23/21', url: 'https://docs.google.com/spreadsheets/d/1Pk64XwoEWeqYVt8_CJAV0DfeOdxU7Uk3M7glmmUW_PQ/edit?usp=sharing', sheetId: '1Pk64XwoEWeqYVt8_CJAV0DfeOdxU7Uk3M7glmmUW_PQ', type: 'gsheet' },
  { id: '2021-side-respawn-h4-2', category: 'Side-2021', year: 2021, title: 'The Respawn All-Stars Halo 4 Grifball Fall Series (2/3) 11/13/21', url: 'https://docs.google.com/spreadsheets/d/1DAklsSydfZ--N0Frsb3oXlksTidq08Dp/edit?usp=sharing&ouid=115452215758315537570&rtpof=true&sd=true', sheetId: '1DAklsSydfZ--N0Frsb3oXlksTidq08Dp', type: 'gsheet' },
  { id: '2021-side-respawn-h4-3', category: 'Side-2021', year: 2021, title: 'The Respawn All-Stars Halo 4 Grifball Fall Series (3/3) 12/04/21', url: 'https://docs.google.com/spreadsheets/d/1mKxH9c5OmXN_khsivQyx85vufJsPYTw6/edit?usp=sharing&ouid=115452215758315537570&rtpof=true&sd=true', sheetId: '1mKxH9c5OmXN_khsivQyx85vufJsPYTw6', type: 'gsheet' },

  // === Side Tournaments Undated ===
  { id: 'side-slide-league', category: 'Side-Undated', year: null, title: 'Slide League Schedule and Rosters', url: 'https://docs.google.com/spreadsheets/d/115WFH-xdHD4VdTFOclcizC1LqAQhZ5dtn1g0O1xyPAI/edit?usp=sharing', sheetId: '115WFH-xdHD4VdTFOclcizC1LqAQhZ5dtn1g0O1xyPAI', type: 'gsheet' },
  { id: 'side-original-late-league-rankings', category: 'Side-Undated', year: null, title: 'Original Late League Rankings', url: 'https://docs.google.com/spreadsheets/d/15nK7uU3dM37CXFl0y4xRNMMIqsWCZKxhoH2SG5hhC64/edit#gid=1045466237', sheetId: '15nK7uU3dM37CXFl0y4xRNMMIqsWCZKxhoH2SG5hhC64', gid: '1045466237', type: 'gsheet' },
  { id: 'side-bum-rush', category: 'Side-Undated', year: null, title: 'Bum Rush Spreadsheet', url: 'https://docs.google.com/spreadsheets/d/1M5vE-uRCkpVCRYcJ1hPocOq4O7Jg7fTyhBtHQTjI2NM/edit#gid=0', sheetId: '1M5vE-uRCkpVCRYcJ1hPocOq4O7Jg7fTyhBtHQTjI2NM', gid: '0', type: 'gsheet' },
  { id: 'side-reach-pc-stats', category: 'Side-Undated', year: null, title: 'Reach PC Stats', url: 'https://docs.google.com/spreadsheets/d/1svJJBo_jc-ThGabZG-laXX6M8PT0CF9NJYwILq_kEEk/htmlview', sheetId: '1svJJBo_jc-ThGabZG-laXX6M8PT0CF9NJYwILq_kEEk', type: 'gsheet' },

  // === Late League, VODs, History, Websites ===
  { id: 'late-league-website', category: 'Late-League', year: null, title: 'Late League Website', url: 'https://www.lateleague.org', type: 'web' },
  { id: 'late-league-aspence-main', category: 'Late-League', year: null, title: 'ASpence Late League Main Sheet', url: 'https://docs.google.com/spreadsheets/d/1G1zXn-ddqu3f183x8My8w2pG7_SyyVcz/edit?usp=sharing&ouid=115452215758315537570&rtpof=true&sd=true', sheetId: '1G1zXn-ddqu3f183x8My8w2pG7_SyyVcz', type: 'gsheet' },
  { id: 'youtube-grifball-channel', category: 'VODs', year: null, title: 'Grifball YouTube Channel', url: 'https://www.youtube.com/channel/UCTVJapRl-GGwLCfioTb9N8w', channelId: 'UCTVJapRl-GGwLCfioTb9N8w', type: 'youtube' },
  { id: 'history-agla-champions', category: 'History', year: null, title: 'AGLA Champions', url: 'https://docs.google.com/spreadsheets/d/1SlO5AU2U0fT8rhik8uarA0TTbVB0TPu5Xm_BZpKsCZg/edit?gid=0#gid=0', sheetId: '1SlO5AU2U0fT8rhik8uarA0TTbVB0TPu5Xm_BZpKsCZg', gid: '0', type: 'gsheet' },
  { id: 'history-all-time-infinite-stats-luxe', category: 'History', year: null, title: 'All-time Infinite Stats by Luxe', url: 'https://docs.google.com/spreadsheets/d/1Y3NPRp004pzB57-vSysnyhyhMgqM3Yz0Vh_UnU2yIS8/edit?gid=96634953#gid=96634953', sheetId: '1Y3NPRp004pzB57-vSysnyhyhMgqM3Yz0Vh_UnU2yIS8', gid: '96634953', type: 'gsheet' },
  { id: 'website-playgrifball', category: 'Website', year: null, title: 'PlayGrifball', url: 'http://playgrifball.com/', type: 'web' },
  { id: 'merch-teespring-agla', category: 'Merchandise', year: null, title: 'Tee Spring Store', url: 'https://teespring.com/stores/agla-store', type: 'web' },
];

const UA = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36';

async function fetchWithTimeout(url, options = {}, timeoutMs = 25000) {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), timeoutMs);
  try {
    const res = await fetch(url, {
      ...options,
      headers: { 'User-Agent': UA, ...(options.headers || {}) },
      redirect: 'follow',
      signal: controller.signal,
    });
    const buf = Buffer.from(await res.arrayBuffer());
    return { ok: res.ok, status: res.status, contentType: res.headers.get('content-type') || '', finalUrl: res.url, buffer: buf };
  } catch (err) {
    return { ok: false, status: 0, error: err.message, contentType: '', finalUrl: url, buffer: Buffer.alloc(0) };
  } finally {
    clearTimeout(timer);
  }
}

function isZipOrXlsx(buf) {
  return buf.length > 4 && buf[0] === 0x50 && buf[1] === 0x4b && buf[2] === 0x03 && buf[3] === 0x04;
}

async function tryWayback(targetUrl, baseFilePath) {
  const cdxUrl = `https://web.archive.org/cdx/search/cdx?url=${encodeURIComponent(targetUrl)}&output=json&limit=10&filter=statuscode:200`;
  const cdx = await fetchWithTimeout(cdxUrl, {}, 15000);
  if (!cdx.ok || cdx.buffer.length === 0) return null;
  try {
    const rows = JSON.parse(cdx.buffer.toString('utf8'));
    if (!Array.isArray(rows) || rows.length < 2) return null;
    // Take the most recent snapshot
    const last = rows[rows.length - 1];
    const timestamp = last[1];
    const orig = last[2];
    const wbUrl = `https://web.archive.org/web/${timestamp}id_/${orig}`;
    const snap = await fetchWithTimeout(wbUrl, {}, 25000);
    if (snap.ok && snap.buffer.length > 0) {
      await fs.writeFile(`${baseFilePath}.wayback.html`, snap.buffer);
      return { timestamp, wbUrl, bytes: snap.buffer.length };
    }
  } catch {
    // ignore JSON parse errors
  }
  return null;
}

async function downloadGsheet(item) {
  const { id, sheetId, gid, url } = item;
  const base = path.join(RAW_DIR, id);
  const result = { id, title: item.title, category: item.category, year: item.year, url, type: item.type, formats: [] };

  // 1. Try export?format=xlsx
  const exportUrl = `https://docs.google.com/spreadsheets/d/${sheetId}/export?format=xlsx`;
  const res1 = await fetchWithTimeout(exportUrl);
  if (res1.ok && isZipOrXlsx(res1.buffer)) {
    await fs.writeFile(`${base}.xlsx`, res1.buffer);
    result.formats.push({ format: 'xlsx', bytes: res1.buffer.length, source: 'gsheet-export' });
    return result;
  }

  // 2. Try Google Drive direct download (for native .xlsx uploaded to Drive with rtpof=true)
  const driveUrl = `https://drive.google.com/uc?export=download&id=${sheetId}`;
  const res2 = await fetchWithTimeout(driveUrl);
  if (res2.ok && isZipOrXlsx(res2.buffer)) {
    await fs.writeFile(`${base}.xlsx`, res2.buffer);
    result.formats.push({ format: 'xlsx', bytes: res2.buffer.length, source: 'gdrive-uc' });
    return result;
  }

  // 3. Try htmlview (works even when export is restricted or for view-only sheets)
  const htmlViewUrl = `https://docs.google.com/spreadsheets/d/${sheetId}/htmlview`;
  const res3 = await fetchWithTimeout(htmlViewUrl);
  if (res3.ok && res3.buffer.length > 500 && !res3.finalUrl.includes('accounts.google.com')) {
    const htmlStr = res3.buffer.toString('utf8');
    if (htmlStr.includes('<table') || htmlStr.includes('waffle')) {
      await fs.writeFile(`${base}.html`, res3.buffer);
      result.formats.push({ format: 'htmlview', bytes: res3.buffer.length });
    }
  }

  // 4. Try CSV export via gviz
  const csvUrl = `https://docs.google.com/spreadsheets/d/${sheetId}/gviz/tq?tqx=out:csv${gid ? `&gid=${gid}` : ''}`;
  const res4 = await fetchWithTimeout(csvUrl);
  if (res4.ok && res4.buffer.length > 10 && !res4.finalUrl.includes('accounts.google.com') && !res4.contentType.includes('text/html')) {
    await fs.writeFile(`${base}.csv`, res4.buffer);
    result.formats.push({ format: 'csv', bytes: res4.buffer.length });
  }

  // 5. If nothing worked, record HTTP status & check Wayback Machine
  if (result.formats.length === 0) {
    result.status = res1.status || res2.status || res3.status;
    result.finalUrl = res1.finalUrl;
    const wb = await tryWayback(url, base) || await tryWayback(`https://docs.google.com/spreadsheets/d/${sheetId}*`, base);
    if (wb) {
      result.formats.push({ format: 'wayback', ...wb });
    }
  }
  return result;
}

async function downloadOneDrive(item) {
  const base = path.join(RAW_DIR, item.id);
  const result = { id: item.id, title: item.title, category: item.category, year: item.year, url: item.url, type: item.type, formats: [] };

  // Encode sharing URL for OneDrive API v1.0 shares endpoint: u!base64url
  const b64 = Buffer.from(item.url, 'utf8').toString('base64').replace(/=+$/, '').replace(/\//g, '_').replace(/\+/g, '-');
  const sharesApiUrl = `https://api.onedrive.com/v1.0/shares/u!${b64}/root/content`;
  const resApi = await fetchWithTimeout(sharesApiUrl);
  if (resApi.ok && isZipOrXlsx(resApi.buffer)) {
    await fs.writeFile(`${base}.xlsx`, resApi.buffer);
    result.formats.push({ format: 'xlsx', bytes: resApi.buffer.length, source: 'onedrive-shares-api' });
    return result;
  }

  // Fallback: follow redirect on original URL and try download=1
  const resPage = await fetchWithTimeout(item.url);
  if (resPage.ok && isZipOrXlsx(resPage.buffer)) {
    await fs.writeFile(`${base}.xlsx`, resPage.buffer);
    result.formats.push({ format: 'xlsx', bytes: resPage.buffer.length, source: 'onedrive-direct' });
    return result;
  }
  if (resPage.finalUrl) {
    const dlUrl = resPage.finalUrl.includes('?') ? `${resPage.finalUrl}&download=1` : `${resPage.finalUrl}?download=1`;
    const resDl = await fetchWithTimeout(dlUrl);
    if (resDl.ok && isZipOrXlsx(resDl.buffer)) {
      await fs.writeFile(`${base}.xlsx`, resDl.buffer);
      result.formats.push({ format: 'xlsx', bytes: resDl.buffer.length, source: 'onedrive-download1' });
      return result;
    }
    await fs.writeFile(`${base}.html`, resPage.buffer);
    result.formats.push({ format: 'html', bytes: resPage.buffer.length, finalUrl: resPage.finalUrl, apiStatus: resApi.status });
  }
  return result;
}

async function downloadChallonge(item) {
  const base = path.join(RAW_DIR, item.id);
  const result = { id: item.id, title: item.title, category: item.category, year: item.year, url: item.url, type: item.type, formats: [] };

  const res = await fetchWithTimeout(item.url);
  if (res.ok && res.buffer.length > 0) {
    await fs.writeFile(`${base}.html`, res.buffer);
    result.formats.push({ format: 'html', bytes: res.buffer.length });
  } else {
    result.status = res.status;
  }
  // Also try .json and .svg endpoints on Challonge and Wayback Machine
  const resJson = await fetchWithTimeout(`${item.url}.json`);
  if (resJson.ok && resJson.buffer.length > 0 && resJson.contentType.includes('json')) {
    await fs.writeFile(`${base}.json`, resJson.buffer);
    result.formats.push({ format: 'json', bytes: resJson.buffer.length });
  }
  const wb = await tryWayback(item.url, base);
  if (wb) {
    result.formats.push({ format: 'wayback', ...wb });
  }
  return result;
}

async function downloadYouTube(item) {
  const base = path.join(RAW_DIR, item.id);
  const result = { id: item.id, title: item.title, category: item.category, year: item.year, url: item.url, type: item.type, formats: [] };
  const rssUrl = `https://www.youtube.com/feeds/videos.xml?channel_id=${item.channelId}`;
  const resRss = await fetchWithTimeout(rssUrl);
  if (resRss.ok && resRss.buffer.length > 0) {
    await fs.writeFile(`${base}.rss.xml`, resRss.buffer);
    result.formats.push({ format: 'rss-xml', bytes: resRss.buffer.length });
  }
  const resPage = await fetchWithTimeout(item.url);
  if (resPage.ok && resPage.buffer.length > 0) {
    await fs.writeFile(`${base}.html`, resPage.buffer);
    result.formats.push({ format: 'html', bytes: resPage.buffer.length });
  }
  return result;
}

async function downloadWeb(item) {
  const base = path.join(RAW_DIR, item.id);
  const result = { id: item.id, title: item.title, category: item.category, year: item.year, url: item.url, type: item.type, formats: [] };
  const res = await fetchWithTimeout(item.url);
  if (res.ok && res.buffer.length > 0) {
    await fs.writeFile(`${base}.html`, res.buffer);
    result.formats.push({ format: 'html', bytes: res.buffer.length, finalUrl: res.finalUrl });
  } else {
    result.status = res.status;
    result.error = res.error;
  }
  const wb = await tryWayback(item.url, base);
  if (wb) {
    result.formats.push({ format: 'wayback', ...wb });
  }
  return result;
}

async function main() {
  await fs.mkdir(RAW_DIR, { recursive: true });
  console.log(`Downloading ${SOURCES.length} historical Grifball sources...`);

  const results = [];
  const concurrency = 6;
  for (let i = 0; i < SOURCES.length; i += concurrency) {
    const batch = SOURCES.slice(i, i + concurrency);
    const batchResults = await Promise.all(batch.map(async item => {
      try {
        let r;
        if (item.type === 'gsheet') r = await downloadGsheet(item);
        else if (item.type === 'onedrive') r = await downloadOneDrive(item);
        else if (item.type === 'challonge') r = await downloadChallonge(item);
        else if (item.type === 'youtube') r = await downloadYouTube(item);
        else r = await downloadWeb(item);
        console.log(`[${r.formats.length > 0 ? 'OK' : 'MISS'}] ${item.id} (${item.title}) -> ${r.formats.map(f => `${f.format}:${f.bytes}`).join(', ') || r.status || r.error}`);
        return r;
      } catch (err) {
        console.error(`[ERR] ${item.id}: ${err.message}`);
        return { id: item.id, title: item.title, url: item.url, formats: [], error: err.message };
      }
    }));
    results.push(...batchResults);
  }

  await fs.writeFile(path.join(OUT_DIR, 'download-report.json'), JSON.stringify(results, null, 2));
  const okCount = results.filter(r => r.formats.length > 0).length;
  console.log(`\nCompleted: ${okCount}/${SOURCES.length} sources downloaded.`);
}

if (process.argv[1] && process.argv[1].endsWith('download-all.mjs')) {
  main().catch(err => {
    console.error(err);
    process.exit(1);
  });
}
