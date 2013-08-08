/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-60.js
 * @description String.prototype.trim handles whitepace and lineterminators (string with just blanks)
 */


function testcase() {
  if ("    ".trim() === "") {
    return true;
  }
 }
runTestCase(testcase);
