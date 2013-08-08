/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-2.js
 * @description Array.prototype.indexOf when fromIndex is floating point number
 */


function testcase() {
  var a = new Array(1,2,3);
  if (a.indexOf(3,0.49) === 2 &&    // 0.49 resolves to 0
      a.indexOf(1,0.51) === 0 &&    // 0.51 resolves to 0
      a.indexOf(1,1.51) === -1) {   // 1.01 resolves to 1
    return true;
  }
 }
runTestCase(testcase);
