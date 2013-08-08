/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * This test should be run without any built-ins being added/augmented.
 * The name JSON must be bound to an object.
 * 
 * Section 15 says that every built-in Function object described in this
 * section � whether as a constructor, an ordinary function, or both � has
 * a length property whose value is an integer. Unless otherwise specified,
 * this value is equal to the largest number of named arguments shown in
 * the section headings for the function description, including optional
 * parameters.
 * 
 * This default applies to JSON.parse, and it must exist as a function
 * taking 2 parameters.
 *
 * @path ch15/15.12/15.12.2/15.12.2-0-2.js
 * @description JSON.parse must exist as a function taking 2 parameters
 */


function testcase() {
  var f = JSON.parse;

  if (typeof(f) === "function" && f.length === 2) {
    return true;
  }
 }
runTestCase(testcase);
