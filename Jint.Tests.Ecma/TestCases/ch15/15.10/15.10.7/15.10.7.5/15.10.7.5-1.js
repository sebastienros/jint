/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.10/15.10.7/15.10.7.5/15.10.7.5-1.js
 * @description RegExp.prototype.lastIndex is of type Number
 */


function testcase() {
  return (typeof(RegExp.prototype.lastIndex)) === 'number';
 }
runTestCase(testcase);
