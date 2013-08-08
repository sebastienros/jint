/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-3.js
 * @description Array.prototype.indexOf when fromIndex is boolean
 */


function testcase() {
  var a = [1,2,3];
  if (a.indexOf(1,true) === -1 &&        // true resolves to 1
     a.indexOf(1,false) === 0 ) {       // false resolves to 0
    return true;
  }
 }
runTestCase(testcase);
