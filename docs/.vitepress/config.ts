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
          {
            text: 'Core',
            items: [
              { text: 'MonadicSharp', link: 'https://danny4897.github.io/MonadicSharp/' },
              { text: 'MonadicSharp.Framework', link: 'https://danny4897.github.io/MonadicSharp.Framework/' },
            ],
          },
          {
            text: 'Extensions',
            items: [
              { text: 'MonadicSharp.AI', link: 'https://danny4897.github.io/MonadicSharp.AI/' },
              { text: 'MonadicSharp.Recovery', link: 'https://danny4897.github.io/MonadicSharp.Recovery/' },
              { text: 'MonadicSharp.Azure', link: 'https://danny4897.github.io/MonadicSharp.Azure/' },
              { text: 'MonadicSharp.DI', link: 'https://danny4897.github.io/MonadicSharp.DI/' },
            ],
          },
          {
            text: 'Tooling',
            items: [
              { text: 'MonadicLeaf', link: 'https://danny4897.github.io/MonadicLeaf/' },
              { text: 'MonadicSharp × OpenCode', link: 'https://danny4897.github.io/MonadicSharp-OpenCode/' },
              { text: 'AgentScope', link: 'https://danny4897.github.io/AgentScope/' },
            ],
          },
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
