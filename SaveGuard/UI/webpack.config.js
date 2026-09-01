const path = require('path');
const webpack = require('webpack');
const manifest = require('./mod.json');
// CS2 parses this header inside the .mjs; mod.json alone does not register the UI.
// Its version parser is System.Version, so keep prerelease labels in mod.json.
const moduleBanner = `/**\n * Cities: Skylines II UI Module\n * Id: ${manifest.id}\n * Author: ${manifest.author}\n * Version: ${manifest.version.split('-')[0]}\n * Dependencies: ${(manifest.dependencies || []).join(', ')}\n */`;
module.exports = {
  plugins: [new webpack.BannerPlugin({ banner: moduleBanner, raw: true, stage: webpack.Compilation.PROCESS_ASSETS_STAGE_REPORT })],
  entry: './src/index.tsx',
  output: {
    path: path.resolve(__dirname, 'dist'),
    filename: 'SaveGuard.mjs',
    library: { type: 'module' },
    publicPath: 'coui://ui-mods/'
  },
  module: {
    rules: [
      { test: /\.tsx?$/, use: { loader: 'ts-loader', options: { transpileOnly: true } }, exclude: /node_modules/ },
      { test: /\.s?css$/, use: ['style-loader', 'css-loader', 'sass-loader'] },
      { test: /\.(png|jpe?g|gif|svg)$/i, type: 'asset/resource', generator: { filename: 'images/[name][ext]' } }
    ]
  },
  resolve: { extensions: ['.tsx', '.ts', '.js'] },
  externalsType: 'window',
  externals: {
    react: 'React',
    'react-dom': 'ReactDOM',
    'react-dom/client': 'ReactDOM',
    'cs2/modding': 'cs2/modding',
    'cs2/api': 'cs2/api',
    'cs2/ui': 'cs2/ui'
  },
  experiments: { outputModule: true }
};
