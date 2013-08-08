/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-4-6.js
 * @description Array.prototype.indexOf returns -1 if 'length' is 0 (subclassed Array, length overridden with obj with valueOf)
 */


function testcase() {
  
 var i = Array.prototype.indexOf.call({length: { valueOf: function () { return 0;}}}, 1);
  
  if (i === -1) {
    return true;
  }
 }
runTestCase(testcase);
