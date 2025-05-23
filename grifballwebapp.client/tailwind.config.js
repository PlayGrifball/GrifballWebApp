/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./src/**/*.{html,ts}",
  ],
  safelist: [
    {
      pattern: /bg-(primary|secondary|tertiary|neutral|neutral-variant|error)/,
    },
    {
      pattern: /bg-(primary|secondary|tertiary|neutral|neutral-variant|error)-(0|10|20|30|40|50|60|70|80|90|100)/,
    }
  ],
  theme: {
    extend: {
      colors: {
        "primary": 'var(--primary)',
        "primary-0": 'var(--primary-0)',
        "primary-10": 'var(--primary-10)',
        "primary-20": 'var(--primary-20)',
        "primary-30": 'var(--primary-30)',
        "primary-40": 'var(--primary-40)',
        "primary-50": 'var(--primary-50)',
        "primary-60": 'var(--primary-60)',
        "primary-70": 'var(--primary-70)',
        "primary-80": 'var(--primary-80)',
        "primary-90": 'var(--primary-90)',
        "primary-100": 'var(--primary-100)',

        "secondary": 'var(--secondary)',
        "secondary-0": 'var(--secondary-0)',
        "secondary-10": 'var(--secondary-10)',
        "secondary-20": 'var(--secondary-20)',
        "secondary-30": 'var(--secondary-30)',
        "secondary-40": 'var(--secondary-40)',
        "secondary-50": 'var(--secondary-50)',
        "secondary-60": 'var(--secondary-60)',
        "secondary-70": 'var(--secondary-70)',
        "secondary-80": 'var(--secondary-80)',
        "secondary-90": 'var(--secondary-90)',
        "secondary-100": 'var(--secondary-100)',

        "tertiary": 'var(--tertiary)',
        "tertiary-0": 'var(--tertiary-0)',
        "tertiary-10": 'var(--tertiary-10)',
        "tertiary-20": 'var(--tertiary-20)',
        "tertiary-30": 'var(--tertiary-30)',
        "tertiary-40": 'var(--tertiary-40)',
        "tertiary-50": 'var(--tertiary-50)',
        "tertiary-60": 'var(--tertiary-60)',
        "tertiary-70": 'var(--tertiary-70)',
        "tertiary-80": 'var(--tertiary-80)',
        "tertiary-90": 'var(--tertiary-90)',
        "tertiary-100": 'var(--tertiary-100)',
		
		    "neutral": 'var(--neutral)',
        "neutral-0": 'var(--neutral-0)',
        "neutral-10": 'var(--neutral-10)',
        "neutral-20": 'var(--neutral-20)',
        "neutral-30": 'var(--neutral-30)',
        "neutral-40": 'var(--neutral-40)',
        "neutral-50": 'var(--neutral-50)',
        "neutral-60": 'var(--neutral-60)',
        "neutral-70": 'var(--neutral-70)',
        "neutral-80": 'var(--neutral-80)',
        "neutral-90": 'var(--neutral-90)',
        "neutral-100": 'var(--neutral-100)',
		
		    "neutral-variant": 'var(--neutral-variant)',
        "neutral-variant-0": 'var(--neutral-variant-0)',
        "neutral-variant-10": 'var(--neutral-variant-10)',
        "neutral-variant-20": 'var(--neutral-variant-20)',
        "neutral-variant-30": 'var(--neutral-variant-30)',
        "neutral-variant-40": 'var(--neutral-variant-40)',
        "neutral-variant-50": 'var(--neutral-variant-50)',
        "neutral-variant-60": 'var(--neutral-variant-60)',
        "neutral-variant-70": 'var(--neutral-variant-70)',
        "neutral-variant-80": 'var(--neutral-variant-80)',
        "neutral-variant-90": 'var(--neutral-variant-90)',
        "neutral-variant-100": 'var(--neutral-variant-100)',
		
		    "error": 'var(--error)',
        "error-0": 'var(--error-0)',
        "error-10": 'var(--error-10)',
        "error-20": 'var(--error-20)',
        "error-30": 'var(--error-30)',
        "error-40": 'var(--error-40)',
        "error-50": 'var(--error-50)',
        "error-60": 'var(--error-60)',
        "error-70": 'var(--error-70)',
        "error-80": 'var(--error-80)',
        "error-90": 'var(--error-90)',
        "error-100": 'var(--error-100)',
      }
    },
  },
  plugins: [],
}

