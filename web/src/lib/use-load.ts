"use client";
import { useEffect, useState } from "react";
import { api } from "./api";
export function useLoad<T>(path: string | null, revision = 0) {
  const [state, setState] = useState<{
    path: string | null;
    data?: T;
    error?: boolean;
    loading: boolean;
  }>({ path: null, loading: !!path });
  useEffect(() => {
    if (!path) {
      setState({ path, loading: false });
      return;
    }
    let active = true;
    setState({ path, loading: true });
    api<T>(path)
      .then((data) => {
        if (active) setState({ path, data, loading: false });
      })
      .catch(() => {
        if (active) setState({ path, error: true, loading: false });
      });
    return () => {
      active = false;
    };
  }, [path, revision]);
  return state.path === path
    ? state
    : { loading: !!path, data: undefined, error: undefined };
}
