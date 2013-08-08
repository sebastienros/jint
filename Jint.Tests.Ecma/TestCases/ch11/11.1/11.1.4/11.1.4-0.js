/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.1/11.1.4/11.1.4-0.js
 * @description elements elided at the end of an array do not contribute to its length
 */


function testcase() {
  var a = [,];
  if (a.length === 1) {
    return true;
  }
 }
runTestCase(testcase);
