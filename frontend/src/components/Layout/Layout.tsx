import { type ReactNode } from 'react'
import TopBar from './TopBar'
import TabsBar from './TabsBar'
import './Layout.css'

export default function Layout({ children }: { children: ReactNode }) {
  return (
    <>
      <TopBar />
      <TabsBar />
      <div className="tab-content">{children}</div>
    </>
  )
}
