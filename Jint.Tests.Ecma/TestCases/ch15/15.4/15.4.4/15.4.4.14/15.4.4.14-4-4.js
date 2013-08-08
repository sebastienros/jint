/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-4-4.js
 * @description Array.prototype.indexOf returns -1 if 'length' is 0 (generic 'array' with length 0 )
 */


function testcase() {
  
 var i = Array.prototype.lastIndexOf.call({length: 0}, 1);
  
  if (i === -1) {
    return true;
  }
 }
runTestCase(testcase);
