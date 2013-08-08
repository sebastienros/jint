/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.10/15.10.7/15.10.7.4/15.10.7.4-1.js
 * @description RegExp.prototype.multiline is of type Boolean
 */


function testcase() {
  return (typeof(RegExp.prototype.multiline)) === 'boolean';
 }
runTestCase(testcase);
