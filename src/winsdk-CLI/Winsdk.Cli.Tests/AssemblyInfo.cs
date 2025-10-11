// Configure test parallelization at assembly level
[assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]
