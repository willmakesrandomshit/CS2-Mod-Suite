const path = require('path');

module.exports = {
  mode: 'production',
  entry: './src/index.tsx',
  output: {
    path: path.resolve(__dirname, 'dist'),
    filename: 'Parkify.mjs',
    library: {
      type: 'module',
    },
  },
  experiments: {
    outputModule: true,
  },
  externalsType: 'module',
  externals: {
    'cs2/api': 'cs2/api',
    react: 'React',
    'react-dom': 'ReactDOM',
    'cs2/ui': 'cs2/ui',
    'cs2/bindings': 'cs2/bindings',
  },
  resolveLoader: {
    modules: [
      path.resolve(__dirname, '../../FastTrack/UI/node_modules'),
      'node_modules'
    ]
  },
  resolve: {
    extensions: ['.ts', '.tsx', '.js'],
    modules: [
      path.resolve(__dirname, 'node_modules'),
      path.resolve(__dirname, '../../FastTrack/UI/node_modules'),
      'node_modules'
    ]
  },
  module: {
    rules: [
      {
        test: /\.tsx?$/,
        use: [
          {
            loader: 'ts-loader',
            options: {
              transpileOnly: true,
            },
          },
        ],
        exclude: /node_modules/,
      },
      {
        test: /\.s[ac]ss$/i,
        use: ['style-loader', 'css-loader', 'sass-loader'],
      },
      {
        test: /\.svg$/,
        type: 'asset/resource',
        generator: {
          filename: 'images/[name][ext]'
        }
      }
    ],
  },
};
