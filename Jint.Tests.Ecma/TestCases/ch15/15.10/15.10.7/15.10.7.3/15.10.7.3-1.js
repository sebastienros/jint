/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.10/15.10.7/15.10.7.3/15.10.7.3-1.js
 * @description RegExp.prototype.ignoreCase is of type Boolean
 */


function testcase() {
  return (typeof(RegExp.prototype.ignoreCase)) === 'boolean';
 }
runTestCase(testcase);
