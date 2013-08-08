/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-39.js
 * @description String.prototype.trim handles whitepace and lineterminators (ab\u0085c)
 */


function testcase() {
  return "ab\u0085c".trim() === "ab\u0085c";
 }
runTestCase(testcase);
