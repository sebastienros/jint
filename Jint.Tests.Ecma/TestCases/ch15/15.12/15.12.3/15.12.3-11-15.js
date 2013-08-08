/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.12/15.12.3/15.12.3-11-15.js
 * @description Applying JSON.stringify with a replacer function to a function returns the replacer value.
 */


function testcase() {
  return JSON.stringify(function() {}, function(k,v) {return 99}) === '99';
  }
runTestCase(testcase);
