module.exports = {
  root: true,
  env: { browser: true, es2020: true },
  extends: [
    'eslint:recommended',
    'plugin:@typescript-eslint/recommended',
    'plugin:react-hooks/recommended',
  ],
  ignorePatterns: ['dist', '.eslintrc.cjs'],
  parser: '@typescript-eslint/parser',
  rules: {
    // We deliberately co-locate hooks/helpers with components (e.g. useAuth with
    // AuthProvider). This rule only affects dev-time Fast Refresh granularity, not
    // the build, so it's off rather than forcing extra files.
    '@typescript-eslint/no-explicit-any': 'off',
  },
}
