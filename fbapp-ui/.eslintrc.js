const path = require('path');

module.exports = {
    root: true,

    parserOptions: {
        extraFileExtensions: ['.vue'],
        parser: '@typescript-eslint/parser',
        project: path.resolve(__dirname, './tsconfig.json'),
        tsconfigRootDir: __dirname,
        ecmaVersion: 2018,
        sourceType: 'module'
    },

    env: {
        browser: true
    },

    extends: [
        'eslint:recommended',
        'plugin:@typescript-eslint/recommended',
        'plugin:@typescript-eslint/recommended-requiring-type-checking',
        'plugin:vue/vue3-recommended',
        'standard'
    ],

    plugins: [
        '@typescript-eslint',
        'vue'
    ],

    globals: {
        ga: 'readonly',
        cordova: 'readonly',
        __statics: 'readonly',
        __QUASAR_SSR__: 'readonly',
        __QUASAR_SSR_SERVER__: 'readonly',
        __QUASAR_SSR_CLIENT__: 'readonly',
        __QUASAR_SSR_PWA__: 'readonly',
        process: 'readonly',
        Capacitor: 'readonly',
        chrome: 'readonly'
    },

    rules: {
        'generator-star-spacing': 'off',
        'arrow-parens': 'off',
        'one-var': 'off',
        'no-void': 'off',
        'multiline-ternary': 'off',

        'import/first': 'off',
        'import/named': 'error',
        'import/namespace': 'error',
        'import/default': 'error',
        'import/export': 'error',
        'import/extensions': 'off',
        'import/no-unresolved': 'off',
        'import/no-extraneous-dependencies': 'off',
        'prefer-promise-reject-errors': 'off',

        indent: ['error', 4, {
            SwitchCase: 1
        }],

        quotes: ['error', 'single', { avoidEscape: true }],
        semi: ['error', 'never'],

        '@typescript-eslint/explicit-function-return-type': 'off',
        '@typescript-eslint/explicit-module-boundary-types': 'off',

        'no-debugger': process.env.NODE_ENV === 'production' ? 'error' : 'off',

        'vue/html-indent': ['error', 4],

        'brace-style': ['error', '1tbs'],
        'comma-dangle': ['warn', 'never'],
        'curly': ['error', 'all'],

        'no-unused-vars': ['warn', {
            args: 'none'
        }],

        'vue/require-default-prop': 'off',
        'unicode-bom': 'off'
    }
}
