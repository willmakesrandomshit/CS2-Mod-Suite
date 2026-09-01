const path = require('path');
module.exports = {
  entry: './src/index.tsx',
  output: { path: path.resolve(__dirname, 'dist'), filename: 'DiscordRPC.mjs', library: { type: 'module' }, publicPath: 'coui://ui-mods/' },
  module: { rules: [
    { test: /\.tsx?$/, use: { loader: 'ts-loader', options: { transpileOnly: true } }, exclude: /node_modules/ },
    { test: /\.s?css$/, use: ['style-loader', 'css-loader', 'sass-loader'] },
    { test: /\.(png|jpe?g|gif|svg)$/i, type: 'asset/resource', generator: { filename: 'images/[name][ext]' } }
  ]},
  resolve: { extensions: ['.tsx', '.ts', '.js'] },
  externalsType: 'window',
  externals: { react: 'React', 'react-dom': 'ReactDOM', 'react-dom/client': 'ReactDOM', 'cs2/modding': 'cs2/modding', 'cs2/api': 'cs2/api', 'cs2/ui': 'cs2/ui' },
  experiments: { outputModule: true }
};
