/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-8-1.js
 * @description Array.prototype.indexOf with negative fromIndex
 */


function testcase() {
  var a = new Array(1,2,3);
  
  if (a.indexOf(2,-1) === -1 &&  
      a.indexOf(2,-2) === 1 &&  
      a.indexOf(1,-3) === 0 &&  
      a.indexOf(1,-5.3) === 0 ) {
    return true;
  }
 }
runTestCase(testcase);
