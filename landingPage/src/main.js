import { mount } from "svelte";
import "bootstrap/dist/css/bootstrap.min.css";
import "bootstrap/dist/js/bootstrap.bundle.min.js";
import App from "./App.svelte";
import "./styles/landing.css";

mount(App, {
  target: document.getElementById("app")
});
