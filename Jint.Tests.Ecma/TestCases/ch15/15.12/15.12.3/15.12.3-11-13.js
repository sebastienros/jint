/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.12/15.12.3/15.12.3-11-13.js
 * @description A JSON.stringify replacer function applied to a top level scalar can return an Object.
 */


function testcase() {
  return JSON.stringify(42, function(k, v) { return v==42 ? {forty:2}: v}) === '{"forty":2}';
  }
runTestCase(testcase);
