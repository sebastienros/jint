/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-4-5.js
 * @description Array.prototype.lastIndexOf returns -1 if 'length' is 0 ( length overridden to '0' (type conversion))
 */


function testcase() {
  
 var i = Array.prototype.lastIndexOf.call({length: '0'}, 1);
  
  if (i === -1) {
    return true;
  }
 }
runTestCase(testcase);
