/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-5.js
 * @description Array.prototype.lastIndexOf when fromIndex is null
 */


function testcase() {
  var a = new Array(1,2,1);
  if (a.lastIndexOf(2,null) === -1 && a.lastIndexOf(1,null) === 0) {       // null resolves to 0
    return true;
  }
 }
runTestCase(testcase);
