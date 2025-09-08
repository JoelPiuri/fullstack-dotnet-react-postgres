import React, { useEffect, useState } from 'react'

const API = import.meta.env.VITE_API_URL

// ----- Sección genérica para Servicios (igual que antes) -----
function Section({ title, endpoint, fields }) {
  const [items, setItems] = useState([])
  const [form, setForm] = useState({})

  const load = async () => {
    const res = await fetch(`${API}/api/${endpoint}`)
    if (!res.ok) { console.error('Error al cargar', endpoint); return }
    setItems(await res.json())
  }

  useEffect(() => { load() }, [])

  const submit = async (e) => {
    e.preventDefault()
    const res = await fetch(`${API}/api/${endpoint}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(form)
    })
    if (!res.ok) { alert('Error al crear'); return }
    setForm({})
    await load()
  }

  const remove = async (id) => {
    const res = await fetch(`${API}/api/${endpoint}/${id}`, { method: 'DELETE' })
    if (!res.ok) { alert('Error al eliminar'); return }
    await load()
  }

  return (
    <div style={{ border: '1px solid #eee', padding: 16, borderRadius: 8, marginBottom: 24 }}>
      <h2>{title}</h2>

      <form onSubmit={submit} style={{ display: 'flex', gap: 8, marginBottom: 12, flexWrap: 'wrap' }}>
        {fields.map(f => (
          <input
            key={f.name}
            placeholder={f.label}
            value={form[f.name] || ''}
            onChange={e => setForm(prev => ({ ...prev, [f.name]: e.target.value }))}
            style={{ padding: 8 }}
          />
        ))}
        <button>Crear</button>
      </form>

      <table width="100%" border="1" cellPadding="6">
        <thead>
          <tr>
            <th>ID</th>
            {fields.map(f => <th key={f.name}>{f.label}</th>)}
            <th>Acciones</th>
          </tr>
        </thead>
        <tbody>
          {items.map(it => (
            <tr key={it.id}>
              <td>{it.id}</td>
              {fields.map(f => <td key={f.name}>{it[f.name]}</td>)}
              <td><button onClick={() => remove(it.id)}>Eliminar</button></td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

// ----- Sección específica para Clientes con asignación de Servicios -----
function ClientsSection() {
  const [clientes, setClientes] = useState([])
  const [servicios, setServicios] = useState([])
  const [form, setForm] = useState({ nombreCliente: '', correo: '', servicioIds: [] })
  const [loading, setLoading] = useState(false)

  const loadClientes = async () => {
    const res = await fetch(`${API}/api/clientes`)
    if (!res.ok) { console.error('Error al cargar clientes'); return }
    setClientes(await res.json())
  }

  const loadServicios = async () => {
    const res = await fetch(`${API}/api/servicios`)
    if (!res.ok) { console.error('Error al cargar servicios'); return }
    setServicios(await res.json())
  }

  useEffect(() => {
    // Cargamos ambas listas
    loadClientes()
    loadServicios()
  }, [])

  const toggleServicio = (id) => {
    setForm(prev => {
      const set = new Set(prev.servicioIds || [])
      if (set.has(id)) set.delete(id); else set.add(id)
      return { ...prev, servicioIds: Array.from(set) }
    })
  }

  const submit = async (e) => {
    e.preventDefault()

    // Validación: debe existir al menos 1 servicio en el sistema
    if (!servicios || servicios.length === 0) {
      alert('Primero debes crear al menos 1 servicio antes de crear clientes.')
      return
    }
    // Validación: al crear el cliente, debe seleccionar al menos 1 servicio
    if (!form.servicioIds || form.servicioIds.length === 0) {
      alert('Selecciona al menos un servicio para el cliente.')
      return
    }

    setLoading(true)
    try {
      const payload = {
        nombreCliente: form.nombreCliente?.trim(),
        correo: form.correo?.trim(),
        servicioIds: form.servicioIds
      }

      const res = await fetch(`${API}/api/clientes`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      })

      if (!res.ok) {
        const txt = await res.text().catch(() => '')
        console.error('Error al crear cliente:', res.status, txt)
        alert('Error al crear cliente.')
        return
      }

      setForm({ nombreCliente: '', correo: '', servicioIds: [] })
      await loadClientes()
    } finally {
      setLoading(false)
    }
  }

  const remove = async (id) => {
    if (!confirm('¿Eliminar este cliente?')) return
    const res = await fetch(`${API}/api/clientes/${id}`, { method: 'DELETE' })
    if (!res.ok) { alert('Error al eliminar'); return }
    await loadClientes()
  }

  const disableCreate = servicios.length === 0

  return (
    <div style={{ border: '1px solid #eee', padding: 16, borderRadius: 8, marginBottom: 24 }}>
      <h2>Clientes</h2>

      {disableCreate && (
        <div style={{ background: '#fff3cd', border: '1px solid #ffeeba', padding: 10, borderRadius: 6, marginBottom: 12 }}>
          No hay servicios creados. Debes crear al menos uno en la sección <b>Servicios</b> antes de crear clientes.
        </div>
      )}

      <form onSubmit={submit} style={{ display: 'grid', gap: 8, marginBottom: 12, gridTemplateColumns: '1fr 1fr', alignItems: 'start' }}>
        <input
          placeholder="Nombre Cliente"
          value={form.nombreCliente}
          onChange={e => setForm(prev => ({ ...prev, nombreCliente: e.target.value }))}
          style={{ padding: 8 }}
          disabled={disableCreate || loading}
        />
        <input
          placeholder="Correo"
          value={form.correo}
          onChange={e => setForm(prev => ({ ...prev, correo: e.target.value }))}
          style={{ padding: 8 }}
          disabled={disableCreate || loading}
        />

        <div style={{ gridColumn: '1 / span 2', padding: 10, border: '1px solid #eee', borderRadius: 6 }}>
          <div style={{ marginBottom: 6, fontWeight: 'bold' }}>Servicios del cliente (mínimo 1):</div>
          {servicios.map(s => (
            <label key={s.id} style={{ display: 'inline-flex', alignItems: 'center', marginRight: 12, marginBottom: 8 }}>
              <input
                type="checkbox"
                checked={form.servicioIds?.includes(s.id) || false}
                onChange={() => toggleServicio(s.id)}
                disabled={disableCreate || loading}
              />
              <span style={{ marginLeft: 6 }}>{s.nombreServicio}</span>
            </label>
          ))}
        </div>

        <div style={{ gridColumn: '1 / span 2' }}>
          <button disabled={disableCreate || loading}>
            {loading ? 'Creando...' : 'Crear Cliente'}
          </button>
        </div>
      </form>

      <table width="100%" border="1" cellPadding="6">
        <thead>
          <tr>
            <th>ID</th>
            <th>Nombre Cliente</th>
            <th>Correo</th>
            <th>Servicios</th>
            <th>Acciones</th>
          </tr>
        </thead>
        <tbody>
          {clientes.map(c => (
            <tr key={c.id}>
              <td>{c.id}</td>
              <td>{c.nombreCliente}</td>
              <td>{c.correo}</td>
              <td>
                {Array.isArray(c.servicios) && c.servicios.length > 0
                  ? c.servicios.map(s => s.nombreServicio).join(', ')
                  : '-'}
              </td>
              <td><button onClick={() => remove(c.id)}>Eliminar</button></td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

// ----- App -----
export default function App() {
  return (
    <div style={{ maxWidth: 900, margin: '40px auto', fontFamily: 'sans-serif' }}>
      <h1>Administración — Clientes & Servicios</h1>
      <p><b>API:</b> {API}</p>

      {/* Clientes con selección de servicios */}
      <ClientsSection />

      {/* CRUD Servicios (igual que antes) */}
      <Section
        title="Servicios"
        endpoint="servicios"
        fields={[
          { name: 'nombreServicio', label: 'Nombre Servicio' },
          { name: 'descripcion', label: 'Descripción' },
        ]}
      />
    </div>
  )
}
