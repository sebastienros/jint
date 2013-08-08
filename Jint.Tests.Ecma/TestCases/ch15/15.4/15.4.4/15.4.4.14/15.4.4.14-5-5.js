/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-5.js
 * @description Array.prototype.indexOf returns 0 if fromIndex is null
 */


function testcase() {
  var a = [1,2,3];
  if (a.indexOf(1,null) === 0 ) {       // null resolves to 0
    return true;
  }
 }
runTestCase(testcase);
