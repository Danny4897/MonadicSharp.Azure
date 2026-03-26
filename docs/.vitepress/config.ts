import { defineConfig } from 'vitepress'

export default defineConfig({
  title: 'MonadicSharp.Azure',
  description: 'Railway-Oriented Programming for the Azure ecosystem — every SDK call wrapped in Result<T> or Option<T>.',
  base: '/MonadicSharp.Azure/',
  cleanUrls: true,

  head: [
    ['meta', { property: 'og:type', content: 'website' }],
    ['meta', { name: 'twitter:card', content: 'summary' }],
  ],

  themeConfig: {
    logo: '/logo.svg',
    siteTitle: 'MonadicSharp.Azure',

    nav: [
      {
        text: 'Guide',
        items: [
          { text: 'Getting Started', link: '/getting-started' },
          { text: 'Error Mapping', link: '/error-mapping' },
        ],
      },
      {
        text: 'Packages',
        items: [
          { text: 'Core', link: '/packages/core' },
          { text: 'Functions', link: '/packages/functions' },
          { text: 'CosmosDb', link: '/packages/cosmosdb' },
          { text: 'Messaging', link: '/packages/messaging' },
          { text: 'Storage', link: '/packages/storage' },
          { text: 'KeyVault', link: '/packages/keyvault' },
          { text: 'OpenAI', link: '/packages/openai' },
        ],
      },
      {
        text: 'Ecosystem',
        items: [
          { text: 'MonadicSharp Core', link: 'https://danny4897.github.io/MonadicSharp/' },
          { text: 'NuGet', link: 'https://www.nuget.org/packages/MonadicSharp.Azure' },
        ],
      },
    ],

    sidebar: {
      '/': [
        {
          text: 'Guide',
          items: [
            { text: 'Getting Started', link: '/getting-started' },
            { text: 'Error Mapping', link: '/error-mapping' },
          ],
        },
        {
          text: 'Packages',
          items: [
            { text: 'Core', link: '/packages/core' },
            { text: 'Functions', link: '/packages/functions' },
            { text: 'CosmosDb', link: '/packages/cosmosdb' },
            { text: 'Messaging (Service Bus)', link: '/packages/messaging' },
            { text: 'Storage (Blob)', link: '/packages/storage' },
            { text: 'KeyVault', link: '/packages/keyvault' },
            { text: 'OpenAI', link: '/packages/openai' },
          ],
        },
      ],
    },

    socialLinks: [
      { icon: 'github', link: 'https://github.com/Danny4897/MonadicSharp.Azure' },
    ],

    search: { provider: 'local' },

    footer: {
      message: 'Released under the MIT License.',
      copyright: 'Copyright © 2024–2026 Danny4897',
    },

    outline: { level: [2, 3], label: 'On this page' },
  },

  markdown: {
    theme: { light: 'github-light', dark: 'one-dark-pro' },
  },
})
