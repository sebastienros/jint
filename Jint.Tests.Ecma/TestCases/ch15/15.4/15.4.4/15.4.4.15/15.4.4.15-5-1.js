/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-1.js
 * @description Array.prototype.lastIndexOf when fromIndex is string
 */


function testcase() {
  var a = new Array(0,1,1);
  if (a.lastIndexOf(1,"1") === 1 &&          // "1" resolves to 1
      a.lastIndexOf(1,"one") === -1) {       // NaN string resolves to 0
    return true;
  }
 }
runTestCase(testcase);
