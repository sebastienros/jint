/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.10/15.10.7/15.10.7.2/15.10.7.2-1.js
 * @description RegExp.prototype.global is of type Boolean
 */


function testcase() {
  return (typeof(RegExp.prototype.global)) === 'boolean';
 }
runTestCase(testcase);
