/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-7.js
 * @description Array.prototype.indexOf must return correct index (self reference)
 */


function testcase() {
  var a = new Array(0,1,2,3);  
  a[2] = a;
  if (a.indexOf(a) === 2 &&  
      a.indexOf(3) === 3 ) 
  {
    return true;
  }
 }
runTestCase(testcase);
