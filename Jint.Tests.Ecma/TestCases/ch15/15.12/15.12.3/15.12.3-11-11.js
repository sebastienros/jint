/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.12/15.12.3/15.12.3-11-11.js
 * @description A JSON.stringify replacer function applied to a top level Object can return undefined.
 */


function testcase() {
  return JSON.stringify({prop:1}, function(k, v) { return undefined }) === undefined;
  }
runTestCase(testcase);
