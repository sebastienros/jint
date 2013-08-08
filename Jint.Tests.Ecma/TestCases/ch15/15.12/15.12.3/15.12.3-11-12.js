/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.12/15.12.3/15.12.3-11-12.js
 * @description A JSON.stringify replacer function applied to a top level scalar can return an Array.
 */


function testcase() {
  return JSON.stringify(42, function(k, v) { return v==42 ?[4,2]:v }) === '[4,2]';
  }
runTestCase(testcase);
