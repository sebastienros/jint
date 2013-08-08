// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * If ToNumber(value) is NaN, ToInteger(value) returns +0
 *
 * @path ch09/9.4/S9.4_A1.js
 * @description Check what position is defined by Number.NaN in string "abc": "abc".charAt(Number.NaN)
 */

// CHECK#1
if ("abc".charAt(Number.NaN) !== "a") {
  $ERROR('#1: "abc".charAt(Number.NaN) === "a". Actual: ' + ("abc".charAt(Number.NaN)));
}

// CHECK#2
if ("abc".charAt("x") !== "a") {
  $ERROR('#2: "abc".charAt("x") === "a". Actual: ' + ("abc".charAt("x")));
}

