/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-6-1.js
 * @description Array.prototype.lastIndexOf when fromIndex greater than Array.length
 */


function testcase() {
  var a = new Array(1,2,3);
  if (a.lastIndexOf(3,5.4) === 2 &&  
     a.lastIndexOf(3,3.1) === 2 ) {
    return true;
  }
 }
runTestCase(testcase);
