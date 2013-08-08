/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.12/15.12.3/15.12.3-11-2.js
 * @description A JSON.stringify replacer function works is applied to a top level undefined value.
 */


function testcase() {
  return JSON.stringify(undefined, function(k, v) { return "replacement" }) === '"replacement"';
  }
runTestCase(testcase);
