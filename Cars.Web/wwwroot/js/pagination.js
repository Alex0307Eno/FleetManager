// 全域：可在任一頁面呼叫
window.usePagination = function (dataRef, defaultSize = 10) {
    const page = Vue.ref(1);
    const pageSize = defaultSize;
    const paged = Vue.computed(() => {
        const start = (page.value - 1) * pageSize;
        const arr = Array.isArray(dataRef.value) ? dataRef.value : [];
        return arr.slice(start, start + pageSize);
    });
    return { page, pageSize, paged };
};

// 全域：分頁元件
window.Pagination = {
    props: {
        modelValue: { type: Number, required: true }, // v-model 綁定頁碼
        pageSize: { type: Number, default: 10 },
        total: { type: Number, required: true },
    },
    emits: ["update:modelValue"],
    computed: {
        totalPages() {
            return Math.ceil((this.total || 0) / this.pageSize) || 1;
        }
    },
    template: `
    <div class="pagination">
      <button class="btn"
              @click="$emit('update:modelValue', modelValue - 1)"
              :disabled="modelValue <= 1">上一頁</button>

      <span>第 {{ modelValue }} / {{ totalPages }} 頁 （共 {{ total }} 筆）</span>

      <button class="btn"
              @click="$emit('update:modelValue', modelValue + 1)"
              :disabled="modelValue >= totalPages">下一頁</button>
    </div>
  `
};
